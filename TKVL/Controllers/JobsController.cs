using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IServiceProvider _serviceProvider;

        public JobsController(JobPortalDbContext context, IServiceProvider serviceProvider)
        {
            _context = context;
            _serviceProvider = serviceProvider;
        }

        // =================================================================
        // API 1: GET /api/jobs (Lấy toàn bộ vị trí việc làm hiển thị lên Trang Chủ)
        // =================================================================
        [HttpGet]
        public async Task<IActionResult> GetHomeJobs()
        {
            try
            {
                var campaigns = await _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation)
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(c => c.MaPhuongNavigation)
                            .ThenInclude(p => p.MaTpNavigation)
                    .Where(t => t.TrangThai == 1)
                    // 👉 THUẬT TOÁN ĐẨY TOP: Ưu tiên VIP (IsPromoted = true) lên đầu, sau đó mới xét ngày
                    .OrderByDescending(t => t.IsPromoted)
                    .ThenByDescending(t => t.MaTin)
                    .Select(t => new
                    {
                        maTin = t.MaTin,
                        tieuDeChienDich = t.TieuDeChienDich,
                        companyName = t.MaCongTyNavigation!.TenCongTy,
                        logo = t.MaCongTyNavigation!.Logo,
                        deadline = t.NgayHetHan,
                        isPromoted = t.IsPromoted, // Bổ sung cờ VIP để React hiện tag HOT
                        viTris = t.ChiTietViTris.Select(c => new {
                            id = c.MaViTri,
                            title = c.TenViTri,
                            salaryRange = c.Luong,
                            locationName = c.MaPhuongNavigation!.MaTpNavigation!.TenTp
                        }).ToList()
                    })
                    .Take(20) // Phân trang cơ bản hiển thị trang chủ
                    .ToListAsync();

                return Ok(new { success = true, data = campaigns });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 2: GET /api/jobs/search (BỘ LỌC NÂNG CAO ĐA CHIỀU)
        [HttpGet("search")]
        public async Task<IActionResult> SearchJobs([FromQuery] string? keyword, [FromQuery] int? maTP, [FromQuery] int? maPhuong, [FromQuery] int? maNganh)
        {
            try
            {
                var query = _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation)
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(c => c.MaPhuongNavigation)
                            .ThenInclude(p => p.MaTpNavigation)
                    .Where(t => t.TrangThai == 1)
                    .AsQueryable();

                // Lọc theo Keyword
                if (!string.IsNullOrEmpty(keyword))
                {
                    query = query.Where(t => t.TieuDeChienDich.Contains(keyword) ||
                                             t.MaCongTyNavigation.TenCongTy.Contains(keyword) ||
                                             t.ChiTietViTris.Any(c => c.TenViTri.Contains(keyword)));
                }

                // 👉 BỔ SUNG LOGIC LỌC PHƯỜNG/XÃ VÀ THÀNH PHỐ
                if (maPhuong.HasValue)
                {
                    // Nếu người dùng chọn Phường/Xã cụ thể -> Lọc thẳng theo mã phường
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.MaPhuong == maPhuong.Value));
                }
                else if (maTP.HasValue)
                {
                    // Nếu người dùng CHỈ chọn Thành Phố (không chọn Phường) -> Lọc theo Thành Phố
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.MaPhuongNavigation.MaTp == maTP.Value));
                }

                // Lọc theo Ngành nghề
                if (maNganh.HasValue)
                {
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.MaNganh == maNganh.Value));
                }

                // Thực thi thuật toán đẩy Top và lấy dữ liệu
                var results = await query
                    .OrderByDescending(t => t.IsPromoted)
                    .ThenByDescending(t => t.NgayHetHan)
                    .Select(t => new
                    {
                        maTin = t.MaTin,
                        tieuDeChienDich = t.TieuDeChienDich,
                        companyName = t.MaCongTyNavigation!.TenCongTy,
                        logo = t.MaCongTyNavigation!.Logo,
                        deadline = t.NgayHetHan,
                        isPromoted = t.IsPromoted,
                        viTris = t.ChiTietViTris.Select(c => new {
                            id = c.MaViTri,
                            title = c.TenViTri,
                            salaryRange = c.Luong,
                            locationName = c.MaPhuongNavigation!.MaTpNavigation!.TenTp
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
        // =================================================================
        // API 3: GET /api/jobs/{id} (Lấy thông tin CHI TIẾT)
        // =================================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetJobDetail(int id)
        {
            var jobDetail = await _context.TinTuyenDungs
                .Include(t => t.MaCongTyNavigation)
                .Include(t => t.ChiTietViTris)
                    .ThenInclude(c => c.MaPhuongNavigation) // 👉 BỔ SUNG: Join bảng Phường
                        .ThenInclude(p => p.MaTpNavigation) // 👉 BỔ SUNG: Join bảng Thành Phố
                .Where(t => t.MaTin == id)
                .Select(t => new
                {
                    id = t.MaTin,
                    title = t.TieuDeChienDich,
                    companyName = t.MaCongTyNavigation.TenCongTy,
                    logo = t.MaCongTyNavigation.Logo,
                    deadline = t.NgayHetHan,

                    // MAP DANH SÁCH VỊ TRÍ ĐỂ FRONTEND RENDER
                    danhSachViTri = t.ChiTietViTris.Select(v => new
                    {
                        maViTri = v.MaViTri,
                        tenViTri = v.TenViTri,
                        luong = v.Luong,
                        soLuongTuyen = v.SoLuongTuyen,
                        moTaCongViec = v.MoTaCongViec,
                        yeuCauUngVien = v.YeuCauUngVien,
                        quyenLoi = v.QuyenLoi,

                        phuongXa = v.MaPhuongNavigation.TenPhuong,
                        locationName = v.MaPhuongNavigation.MaTpNavigation.TenTp
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (jobDetail == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy chiến dịch tuyển dụng." });
            }

            return Ok(new { success = true, data = jobDetail });
        }
        // =================================================================
        // API 4: POST /api/jobs/{id}/bookmark (Lưu Tin Tuyển Dụng)
        // =================================================================
        [HttpPost("{id}/bookmark")]
        public async Task<IActionResult> ToggleBookmark(int id, [FromHeader] int maUser)
        {
            try
            {
                // Note: Thực tế maUser nên được lấy từ JWT Claims: int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier))
                var existing = await _context.TinDaLuus
                    .FirstOrDefaultAsync(x => x.MaUser == maUser && x.MaViTri == id);

                if (existing != null)
                {
                    _context.TinDaLuus.Remove(existing); // Hủy lưu
                }
                else
                {
                    _context.TinDaLuus.Add(new TinDaLuu
                    {
                        MaUser = maUser,
                        MaViTri = id,
                        NgayLuu = DateTime.Now
                    });
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, isBookmarked = existing == null });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // DTO cho hàm Apply
        // =================================================================
        public class ApplyRequest
        {
            public int MaViTri { get; set; } // 👉 Bắt buộc phải có để hứng dữ liệu từ Dropdown ReactJS
            public int MaCv { get; set; }
            public string ThuGioiThieu { get; set; } = string.Empty;
        }

        // =================================================================
        // API 5: POST  (Nộp Đơn Ứng Tuyển)
        // =================================================================
        [HttpPost("{id}/apply")]
        public async Task<IActionResult> ApplyJob(int id, [FromBody] ApplyRequest request)
        {
            try
            {
                // 1. Xác định User ID một cách an toàn tuyệt đối từ CV thay vì dùng Header
                var cv = await _context.Cvs.FindAsync(request.MaCv);
                if (cv == null)
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy hồ sơ CV trong hệ thống!" });
                }

                int currentUserId = cv.MaUser;

                // 2. Kiểm tra xem ứng viên đã nộp vào vị trí NÀY chưa
                var alreadyApplied = await _context.DonUngTuyens
                    .AnyAsync(d => d.MaViTri == request.MaViTri && d.MaCvNavigation.MaUser == currentUserId);

                if (alreadyApplied)
                {
                    return BadRequest(new { success = false, message = "Bạn đã ứng tuyển vị trí này rồi!" });
                }

                // 3. Tạo đơn ứng tuyển mới với đúng mã vị trí
                var don = new DonUngTuyen
                {
                    MaViTri = request.MaViTri, // 👉 Lưu chuẩn xác vị trí thực tế
                    MaCv = request.MaCv,
                    ThuGioiThieu = request.ThuGioiThieu,
                    NgayNop = DateTime.Now,
                    TrangThai = 0 // 0: Đang chờ duyệt
                };

                _context.DonUngTuyens.Add(don);
                await _context.SaveChangesAsync();

                _ = Task.Run(async () =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var aiService = scope.ServiceProvider.GetRequiredService<IAiAnalysisService>();
                    await aiService.AnalyzeApplicationAsync(don.MaDon);
                });

                return Ok(new { success = true, message = "Ứng tuyển thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
        // =================================================================
        // API 6: GET /api/PhuongXa?maTP=... (Lấy danh sách Phường/Xã theo Thành Phố)
        // =================================================================
        [HttpGet("/api/PhuongXa")] // Đặt fixed route có dấu "/" ở đầu để ghi đè route của Controller
        public async Task<IActionResult> GetPhuongXaByThanhPho([FromQuery] int maTP)
        {
            try
            {
                var phuongXas = await _context.PhuongXas
                    .Where(p => p.MaTp == maTP) // Lọc theo mã Thành Phố
                    .Select(p => new {
                        maPhuong = p.MaPhuong,
                        tenPhuong = p.TenPhuong
                    })
                    .ToListAsync();

                if (!phuongXas.Any())
                {
                    // Trả về mảng rỗng nếu thành phố này chưa có phường xã nào trong DB
                    return Ok(new { success = true, data = new object[] { } });
                }

                return Ok(new { success = true, data = phuongXas });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}