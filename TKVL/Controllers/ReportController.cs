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

                // Tính khoảng thời gian kỳ trước chuẩn xác
                var days = (end.Date - start.Date).Days + 1;
                var prevStart = start.AddDays(-days);
                var prevEnd = start.AddSeconds(-1);

                // ==========================================
                // 1. CHỈ SỐ HIỆN TẠI
                // ==========================================

                // 1.1 Người dùng mới
                var userQuery = _context.Users.Where(u => u.NgayTao >= start && u.NgayTao <= end);
                if (vaiTro.HasValue) userQuery = userQuery.Where(u => u.VaiTro == vaiTro.Value);
                var newUsers = await userQuery.CountAsync();

                // 1.2 Tin tuyển dụng đang hoạt động
                var activeJobs = 0;
                if (!vaiTro.HasValue || vaiTro.Value == 1)
                {
                    activeJobs = await _context.TinTuyenDungs.CountAsync(t => t.TrangThai == 1 && t.NgayHetHan >= DateTime.Now);
                }

                // 1.3 Lượt nộp CV (Có tính theo vai trò NTD / Ứng viên)
                var appQuery = _context.DonUngTuyens
                    .Include(d => d.MaCvNavigation)
                    .Include(d => d.MaViTriNavigation)
                        .ThenInclude(v => v.MaTinNavigation)
                            .ThenInclude(t => t.MaCongTyNavigation)
                    .Where(d => d.NgayNop >= start && d.NgayNop <= end);

                if (vaiTro.HasValue)
                {
                    if (vaiTro.Value == 2) // Ứng viên: đếm đơn của Ứng viên nộp
                        appQuery = appQuery.Where(d => d.MaCvNavigation.MaUserNavigation.VaiTro == 2);
                    else if (vaiTro.Value == 1) // NTD: đếm đơn nộp vào công ty của NTD
                        appQuery = appQuery.Where(d => d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.MaUserNavigation.VaiTro == 1);
                }
                var totalApplications = await appQuery.CountAsync();

                // 1.4 Doanh thu
                var queryGiaoDich = from g in _context.GiaoDiches
                                    join u in _context.Users on g.MaUser equals u.MaUser
                                    where g.TrangThai == true && g.MaGoi != null && g.NgayGd >= start && g.NgayGd <= end
                                    select new { g, u };

                if (maGoi.HasValue) queryGiaoDich = queryGiaoDich.Where(x => x.g.MaGoi == maGoi.Value);
                if (vaiTro.HasValue) queryGiaoDich = queryGiaoDich.Where(x => x.u.VaiTro == vaiTro.Value);

                var totalRevenue = await queryGiaoDich.SumAsync(x => (decimal?)x.g.SoTien) ?? 0;

                // ==========================================
                // 2. CHỈ SỐ KỲ TRƯỚC
                // ==========================================
                var prevUserQuery = _context.Users.Where(u => u.NgayTao >= prevStart && u.NgayTao <= prevEnd);
                if (vaiTro.HasValue) prevUserQuery = prevUserQuery.Where(u => u.VaiTro == vaiTro.Value);
                var prevNewUsers = await prevUserQuery.CountAsync();

                var prevAppQuery = _context.DonUngTuyens
                    .Include(d => d.MaCvNavigation)
                    .Include(d => d.MaViTriNavigation)
                        .ThenInclude(v => v.MaTinNavigation)
                            .ThenInclude(t => t.MaCongTyNavigation)
                    .Where(d => d.NgayNop >= prevStart && d.NgayNop <= prevEnd);

                if (vaiTro.HasValue)
                {
                    if (vaiTro.Value == 2)
                        prevAppQuery = prevAppQuery.Where(d => d.MaCvNavigation.MaUserNavigation.VaiTro == 2);
                    else if (vaiTro.Value == 1)
                        prevAppQuery = prevAppQuery.Where(d => d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.MaUserNavigation.VaiTro == 1);
                }
                var prevApplications = await prevAppQuery.CountAsync();

                var queryGiaoDichPrev = from g in _context.GiaoDiches
                                        join u in _context.Users on g.MaUser equals u.MaUser
                                        where g.TrangThai == true && g.MaGoi != null && g.NgayGd >= prevStart && g.NgayGd <= prevEnd
                                        select new { g, u };

                if (maGoi.HasValue) queryGiaoDichPrev = queryGiaoDichPrev.Where(x => x.g.MaGoi == maGoi.Value);
                if (vaiTro.HasValue) queryGiaoDichPrev = queryGiaoDichPrev.Where(x => x.u.VaiTro == vaiTro.Value);

                var prevRevenue = await queryGiaoDichPrev.SumAsync(x => (decimal?)x.g.SoTien) ?? 0;

                // ==========================================
                // 3. DỮ LIỆU BIỂU ĐỒ TRÒN
                // ==========================================
                var pieUserQuery = _context.Users.Where(u => u.NgayTao >= start && u.NgayTao <= end);
                if (vaiTro.HasValue) pieUserQuery = pieUserQuery.Where(u => u.VaiTro == vaiTro.Value);

                var userRoles = await pieUserQuery
                    .GroupBy(u => u.VaiTro)
                    .Select(g => new {
                        name = g.Key == 1 ? "Nhà tuyển dụng" : g.Key == 2 ? "Ứng viên" : "Admin",
                        value = g.Count()
                    }).ToListAsync();

                var hotIndustries = await (from c in _context.ChiTietViTris
                                           join t in _context.TinTuyenDungs on c.MaTin equals t.MaTin
                                           join n in _context.NganhNgheCons on c.MaNganhCon equals n.MaNganhCon
                                           where t.TrangThai == 1 && t.NgayHetHan >= DateTime.Now
                                           group c by n.TenNganhCon into g
                                           select new { name = g.Key, value = g.Count() })
                                           .OrderByDescending(x => x.value).Take(5).ToListAsync();

                // ==========================================
                // 4. DỮ LIỆU BIỂU ĐỒ CỘT DOANH THU
                // ==========================================
                var rawChartData = await queryGiaoDich
                    .GroupBy(x => x.g.NgayGd.Date)
                    .Select(g => new { RawDate = g.Key, Revenue = g.Sum(x => (decimal?)x.g.SoTien) ?? 0 })
                    .OrderBy(g => g.RawDate).ToListAsync();

                var chartData = rawChartData.Select(g => new { date = g.RawDate.ToString("dd/MM/yyyy"), revenue = g.Revenue }).ToList();

                var packages = await _context.GoiDichVus
                    .Where(g => g.TrangThai == true)
                    .OrderByDescending(g => g.MaGoi)
                    .Select(g => new {
                        maGoi = g.MaGoi,
                        tenGoi = g.TenGoi,
                        doiTuongSuDung = g.DoiTuongSuDung 
                    }).ToListAsync();

                return Ok(new
                {
                    metrics = new { newUsers, activeJobs, totalApplications, totalRevenue },
                    prevMetrics = new { newUsers = prevNewUsers, totalApplications = prevApplications, totalRevenue = prevRevenue },
                    chartData = chartData,
                    pieData = new { userRoles = userRoles, hotIndustries = hotIndustries },
                    packages = packages
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống", error = ex.Message });
            }
        }

        // ==========================================
        // 5. MODAL XEM CHI TIẾT DỮ LIỆU BÁO CÁO
        // ==========================================
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
                        .Select(u => new {
                            Mã_User = u.MaUser,
                            Họ_Tên = u.HoTen,
                            Email = u.Email,
                            Vai_Trò = u.VaiTro == 1 ? "Nhà tuyển dụng" : "Ứng viên",
                            Ngày_Tham_Gia = u.NgayTao
                        })
                        .OrderByDescending(u => u.Ngày_Tham_Gia).ToListAsync();
                    return Ok(data);
                }

                if (type == "jobs")
                {
                    var data = await _context.TinTuyenDungs
                        .Include(t => t.MaCongTyNavigation)
                        .Where(t => t.TrangThai == 1 && t.NgayHetHan >= DateTime.Now)
                        .Select(t => new {
                            Mã_Tin = t.MaTin,
                            Chiến_Dịch = t.TieuDeChienDich,
                            Công_Ty = t.MaCongTyNavigation != null ? t.MaCongTyNavigation.TenCongTy : "Chưa cập nhật",
                            Ngày_Hết_Hạn = t.NgayHetHan
                        })
                        .OrderBy(t => t.Ngày_Hết_Hạn).ToListAsync();
                    return Ok(data);
                }

                if (type == "applications")
                {
                    var appQuery = _context.DonUngTuyens
                        .Include(d => d.MaCvNavigation)
                            .ThenInclude(cv => cv.MaUserNavigation)
                        .Include(d => d.MaViTriNavigation)
                        .Include(d => d.MaViTriNavigation)
                            .ThenInclude(v => v.MaTinNavigation)
                                .ThenInclude(t => t.MaCongTyNavigation)
                        .Where(d => d.NgayNop >= startDate && d.NgayNop <= endDate);

                    if (vaiTro.HasValue)
                    {
                        if (vaiTro.Value == 2)
                            appQuery = appQuery.Where(d => d.MaCvNavigation.MaUserNavigation.VaiTro == 2);
                        else if (vaiTro.Value == 1)
                            appQuery = appQuery.Where(d => d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.MaUserNavigation.VaiTro == 1);
                    }

                    var data = await appQuery
                        .Select(d => new {
                            Mã_Đơn = d.MaDon,
                            Ứng_Viên = d.MaCvNavigation.MaUserNavigation.HoTen,
                            Email_Ứng_Viên = d.MaCvNavigation.MaUserNavigation.Email,
                            Vị_Trí_Ứng_Tuyển = d.MaViTriNavigation.TenViTri,
                            Công_Ty = d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.TenCongTy,
                            Ngày_Nộp = d.NgayNop,
                            Trạng_Thái = d.TrangThai == 0 ? "Tiếp nhận" : d.TrangThai == 1 ? "Đã xem" : d.TrangThai == 2 ? "Phù hợp" : "Chưa phù hợp"
                        })
                        .OrderByDescending(d => d.Ngày_Nộp).ToListAsync();
                    return Ok(data);
                }

                if (type == "revenue")
                {
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
                        Đối_Tượng = x.u.VaiTro == 1 ? "Nhà tuyển dụng" : "Ứng viên",
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