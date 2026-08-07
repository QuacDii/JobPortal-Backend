using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReportController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public ReportController(JobPortalDbContext context)
        {
            _context = context;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboardData(
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate,
            [FromQuery] int? maGoi,
            [FromQuery] int? vaiTro
        )
        {
            try
            {
                var start = startDate ?? DateTime.Now.AddDays(-30);
                var end = endDate ?? DateTime.Now;

                start = start.Date;
                end = end.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

                // Tính khoảng thời gian kỳ trước 
                var duration = (end - start).TotalDays;
                var prevStart = start.AddDays(-duration);
                var prevEnd = start.AddSeconds(-1);

                // --- 1. CHỈ SỐ HIỆN TẠI ---
                var userQuery = _context.Users.Where(u => u.NgayTao >= start && u.NgayTao <= end);
                if (vaiTro.HasValue) userQuery = userQuery.Where(u => u.VaiTro == vaiTro.Value);
                var newUsers = await userQuery.CountAsync();

                var activeJobs = 0;
                if (!vaiTro.HasValue || vaiTro.Value == 1)
                {
                    activeJobs = await _context.TinTuyenDungs.CountAsync(t => t.TrangThai == 1 && t.NgayHetHan >= DateTime.Now);
                }

                var totalApplications = await _context.DonUngTuyens
                    .Where(d => d.NgayNop >= start && d.NgayNop <= end)
                    .CountAsync();

                // 👉 CHỈ TÍNH DOANH THU KHI CÓ GIAO DỊCH MUA GÓI (g.MaGoi != null)
                var queryGiaoDich = from g in _context.GiaoDiches
                                    join u in _context.Users on g.MaUser equals u.MaUser
                                    where g.TrangThai == true && g.MaGoi != null && g.NgayGd >= start && g.NgayGd <= end
                                    select new { g, u };

                if (maGoi.HasValue) queryGiaoDich = queryGiaoDich.Where(x => x.g.MaGoi == maGoi.Value);
                if (vaiTro.HasValue) queryGiaoDich = queryGiaoDich.Where(x => x.u.VaiTro == vaiTro.Value);

                var totalRevenue = await queryGiaoDich.SumAsync(x => (decimal?)x.g.SoTien) ?? 0;

                // --- 2. CHỈ SỐ KỲ TRƯỚC ---
                var prevUserQuery = _context.Users.Where(u => u.NgayTao >= prevStart && u.NgayTao <= prevEnd);
                if (vaiTro.HasValue) prevUserQuery = prevUserQuery.Where(u => u.VaiTro == vaiTro.Value);
                var prevNewUsers = await prevUserQuery.CountAsync();

                var prevApplications = await _context.DonUngTuyens
                    .Where(d => d.NgayNop >= prevStart && d.NgayNop <= prevEnd)
                    .CountAsync();

                var queryGiaoDichPrev = from g in _context.GiaoDiches
                                        join u in _context.Users on g.MaUser equals u.MaUser
                                        where g.TrangThai == true && g.MaGoi != null && g.NgayGd >= prevStart && g.NgayGd <= prevEnd
                                        select new { g, u };

                if (maGoi.HasValue) queryGiaoDichPrev = queryGiaoDichPrev.Where(x => x.g.MaGoi == maGoi.Value);
                if (vaiTro.HasValue) queryGiaoDichPrev = queryGiaoDichPrev.Where(x => x.u.VaiTro == vaiTro.Value);

                var prevRevenue = await queryGiaoDichPrev.SumAsync(x => (decimal?)x.g.SoTien) ?? 0;

                // --- 3. DỮ LIỆU BIỂU ĐỒ TRÒN ---
                var pieUserQuery = _context.Users.Where(u => u.NgayTao >= start && u.NgayTao <= end);
                if (vaiTro.HasValue) pieUserQuery = pieUserQuery.Where(u => u.VaiTro == vaiTro.Value);

                var userRoles = await pieUserQuery
                    .GroupBy(u => u.VaiTro)
                    .Select(g => new {
                        Name = g.Key == 1 ? "Nhà tuyển dụng" : g.Key == 2 ? "Ứng viên" : "Admin",
                        Value = g.Count()
                    }).ToListAsync();

                var hotIndustries = await (from c in _context.ChiTietViTris
                                           join t in _context.TinTuyenDungs on c.MaTin equals t.MaTin
                                           join n in _context.NganhNghes on c.MaNganh equals n.MaNganh
                                           where t.TrangThai == 1 && t.NgayHetHan >= DateTime.Now
                                           group c by n.TenNganh into g
                                           select new { Name = g.Key, Value = g.Count() })
                                           .OrderByDescending(x => x.Value).Take(5).ToListAsync();

                // --- 4. DỮ LIỆU BIỂU ĐỒ CỘT ---
                var rawChartData = await queryGiaoDich
                    .GroupBy(x => x.g.NgayGd.Date)
                    .Select(g => new { RawDate = g.Key, Revenue = g.Sum(x => (decimal?)x.g.SoTien) ?? 0 })
                    .OrderBy(g => g.RawDate).ToListAsync();

                var chartData = rawChartData.Select(g => new { Date = g.RawDate.ToString("dd/MM/yyyy"), Revenue = g.Revenue }).ToList();
                var packages = await _context.GoiDichVus.Select(g => new { g.MaGoi, g.TenGoi }).ToListAsync();

                return Ok(new
                {
                    Metrics = new { newUsers, activeJobs, totalApplications, totalRevenue },
                    PrevMetrics = new { newUsers = prevNewUsers, totalApplications = prevApplications, totalRevenue = prevRevenue },
                    ChartData = chartData,
                    PieData = new { UserRoles = userRoles, HotIndustries = hotIndustries },
                    Packages = packages
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        [HttpGet("details")]
        public async Task<IActionResult> GetDetails(
            [FromQuery] string type,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate,
            [FromQuery] int? maGoi,
            [FromQuery] int? vaiTro
        )
        {
            try
            {
                startDate = startDate.Date;
                endDate = endDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

                if (type == "users")
                {
                    var userQuery = _context.Users.Where(u => u.NgayTao >= startDate && u.NgayTao <= endDate);
                    if (vaiTro.HasValue) userQuery = userQuery.Where(u => u.VaiTro == vaiTro.Value);

                    var data = await userQuery
                        .Select(u => new { ID = u.MaUser, Ten = u.HoTen, Email = u.Email, NgayThamGia = u.NgayTao })
                        .OrderByDescending(u => u.NgayThamGia).ToListAsync();
                    return Ok(data);
                }
                if (type == "jobs")
                {
                    var data = await _context.TinTuyenDungs.Where(t => t.TrangThai == 1 && t.NgayHetHan >= DateTime.Now)
                        .Select(t => new { ID = t.MaTin, TieuDe = t.TieuDeChienDich, NgayHetHan = t.NgayHetHan })
                        .OrderBy(t => t.NgayHetHan).ToListAsync();
                    return Ok(data);
                }
                if (type == "applications")
                {
                    var data = await _context.DonUngTuyens.Where(d => d.NgayNop >= startDate && d.NgayNop <= endDate)
                        .Select(d => new { ID = d.MaDon, NgayNop = d.NgayNop })
                        .OrderByDescending(d => d.NgayNop).ToListAsync();
                    return Ok(data);
                }
                if (type == "revenue")
                {
                    // 👉 CHỈ LẤY GIAO DỊCH MUA GÓI (g.MaGoi != null và Inner Join bắt buộc có gói)
                    var query = from g in _context.GiaoDiches
                                join u in _context.Users on g.MaUser equals u.MaUser
                                join p in _context.GoiDichVus on g.MaGoi equals p.MaGoi
                                where g.TrangThai == true && g.MaGoi != null && g.NgayGd >= startDate && g.NgayGd <= endDate
                                select new { g, u, p };

                    if (maGoi.HasValue) query = query.Where(x => x.g.MaGoi == maGoi.Value);
                    if (vaiTro.HasValue) query = query.Where(x => x.u.VaiTro == vaiTro.Value); 

                    var data = await query.Select(x => new
                    {
                        Mã_GD = x.g.MaGd,
                        Khách_Hàng = x.u.HoTen,
                        Email = x.u.Email,
                        Gói_Đã_Mua = x.p.TenGoi,
                        Số_Tiền = x.g.SoTien,
                        Ngày_Mua = x.g.NgayGd
                    }).OrderByDescending(x => x.Ngày_Mua).ToListAsync();

                    return Ok(data);
                }
                return BadRequest("Loại chi tiết không hợp lệ");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }
    }
}