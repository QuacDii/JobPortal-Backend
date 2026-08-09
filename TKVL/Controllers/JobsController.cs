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
                    .OrderByDescending(t => t.IsPromoted)
                    .ThenByDescending(t => t.MaTin)
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
                            capBac = c.CapBac,
                            salaryRange = c.Luong,
                            locationName = c.MaPhuongNavigation!.MaTpNavigation!.TenTp
                        }).ToList()
                    })
                    .Take(20)
                    .ToListAsync();

                return Ok(new { success = true, data = campaigns });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 2: GET /api/jobs/search (BỘ LỌC NÂNG CAO ĐA CHIỀU HOÀN CHỈNH)
        // =================================================================
        [HttpGet("search")]
        public async Task<IActionResult> SearchJobs(
     [FromQuery] string? keyword,
     [FromQuery] int? maTP,
     [FromQuery] int? maPhuong,
     [FromQuery] string? maNganh,
     [FromQuery] bool? isPromoted,
     [FromQuery] string? mucLuongRadio,
     [FromQuery] decimal? tuLuong,
     [FromQuery] decimal? denLuong,
     [FromQuery] string? kinhNghiem,
     [FromQuery] string? capBac)
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

                // 1. Lọc Pro Company
                if (isPromoted.HasValue && isPromoted.Value)
                {
                    query = query.Where(t => t.IsPromoted == true);
                }

                // 2. Lọc Từ khóa
                if (!string.IsNullOrEmpty(keyword))
                {
                    query = query.Where(t => t.TieuDeChienDich.Contains(keyword) ||
                                             t.MaCongTyNavigation.TenCongTy.Contains(keyword) ||
                                             t.ChiTietViTris.Any(c => c.TenViTri.Contains(keyword)));
                }

                // 3. Lọc Địa điểm
                if (maPhuong.HasValue)
                {
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.MaPhuong == maPhuong.Value));
                }
                else if (maTP.HasValue)
                {
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.MaPhuongNavigation.MaTp == maTP.Value));
                }

                // 4. Lọc Ngành nghề (Hỗ trợ cả Ngành cha & Ngành con)
                if (!string.IsNullOrEmpty(maNganh))
                {
                    var nganhIds = maNganh.Split(',')
                        .Select(x => int.TryParse(x.Trim(), out int id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();

                    if (nganhIds.Any())
                    {
                        var childNganhIds = await _context.NganhNgheCons
                            .Where(c => nganhIds.Contains(c.MaNganhCon) || nganhIds.Contains(c.MaNganhCha))
                            .Select(c => c.MaNganhCon)
                            .ToListAsync();

                        query = query.Where(t => t.ChiTietViTris.Any(c => childNganhIds.Contains(c.MaNganhCon)));
                    }
                }

                // 5. Lọc Cấp bậc
                if (!string.IsNullOrEmpty(capBac))
                {
                    var capBacList = capBac.Split(',').Select(x => x.Trim()).ToList();
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.CapBac != null && capBacList.Contains(c.CapBac)));
                }

                // 6. Lọc Kinh nghiệm
                if (!string.IsNullOrEmpty(kinhNghiem))
                {
                    var expList = kinhNghiem.Split(',').Select(x => x.Trim()).ToList();
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.KinhNghiem != null && expList.Contains(c.KinhNghiem)));
                }

                // Tải danh sách thô về Memory để bóc tách Mức lương chính xác
                var rawResults = await query
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
                            capBac = c.CapBac,
                            kinhNghiem = c.KinhNghiem,
                            salaryRange = c.Luong,
                            locationName = c.MaPhuongNavigation!.MaTpNavigation!.TenTp
                        }).ToList()
                    })
                    .ToListAsync();

                // 7 & 8. LỌC MỨC LƯƠNG CHUẨN XÁC THEO SỐ THỰC
                var filteredResults = rawResults.Where(t => {
                    if (string.IsNullOrEmpty(mucLuongRadio) && !tuLuong.HasValue && !denLuong.HasValue) return true;
                    if (mucLuongRadio == "all" && !tuLuong.HasValue && !denLuong.HasValue) return true;

                    return t.viTris.Any(v => {
                        string luongStr = v.salaryRange ?? "";
                        if (string.IsNullOrEmpty(luongStr)) return false;

                        if (mucLuongRadio == "thoa-thuan") return luongStr.Contains("Thỏa thuận") || luongStr.Contains("Thoả thuận");
                        if (luongStr.Contains("Thỏa thuận") || luongStr.Contains("Thoả thuận")) return false;

                        // Hàm bóc tách số từ chuỗi "15 - 20 Triệu" -> min=15, max=20
                        var numbers = System.Text.RegularExpressions.Regex.Matches(luongStr, @"\d+")
                            .Select(m => decimal.Parse(m.Value))
                            .ToList();

                        decimal jobMin = numbers.Count > 0 ? numbers.Min() : 0;
                        decimal jobMax = numbers.Count > 1 ? numbers.Max() : jobMin;

                        // Xác định khoảng lương yêu cầu
                        decimal reqMin = tuLuong ?? 0;
                        decimal reqMax = denLuong ?? decimal.MaxValue;

                        if (!string.IsNullOrEmpty(mucLuongRadio))
                        {
                            switch (mucLuongRadio)
                            {
                                case "duoi-10": reqMin = 0; reqMax = 10; break;
                                case "10-15": reqMin = 10; reqMax = 15; break;
                                case "15-20": reqMin = 15; reqMax = 20; break;
                                case "20-25": reqMin = 20; reqMax = 25; break;
                                case "25-30": reqMin = 25; reqMax = 30; break;
                                case "30-50": reqMin = 30; reqMax = 50; break;
                                case "tren-50": reqMin = 50; reqMax = decimal.MaxValue; break;
                            }
                        }

                        // Kiểm tra 2 khoảng lương có giao nhau không
                        return (jobMin <= reqMax) && (jobMax >= reqMin);
                    });
                }).ToList();

                return Ok(new { success = true, data = filteredResults });
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
                    .ThenInclude(c => c.MaPhuongNavigation)
                        .ThenInclude(p => p.MaTpNavigation)
                .Where(t => t.MaTin == id)
                .Select(t => new
                {
                    id = t.MaTin,
                    title = t.TieuDeChienDich,
                    companyName = t.MaCongTyNavigation.TenCongTy,
                    logo = t.MaCongTyNavigation.Logo,
                    deadline = t.NgayHetHan,

                    danhSachViTri = t.ChiTietViTris.Select(v => new
                    {
                        maViTri = v.MaViTri,
                        tenViTri = v.TenViTri,
                        luong = v.Luong,
                        capBac = v.CapBac,
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
                var existing = await _context.TinDaLuus
                    .FirstOrDefaultAsync(x => x.MaUser == maUser && x.MaViTri == id);

                if (existing != null)
                {
                    _context.TinDaLuus.Remove(existing);
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
            public int MaViTri { get; set; }
            public int MaCv { get; set; }
            public string ThuGioiThieu { get; set; } = string.Empty;
        }

        // =================================================================
        // API 5: POST (Nộp Đơn Ứng Tuyển)
        // =================================================================
        [HttpPost("{id}/apply")]
        public async Task<IActionResult> ApplyJob(int id, [FromBody] ApplyRequest request)
        {
            try
            {
                var cv = await _context.Cvs.FindAsync(request.MaCv);
                if (cv == null)
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy hồ sơ CV trong hệ thống!" });
                }

                int currentUserId = cv.MaUser;

                var alreadyApplied = await _context.DonUngTuyens
                    .AnyAsync(d => d.MaViTri == request.MaViTri && d.MaCvNavigation.MaUser == currentUserId);

                if (alreadyApplied)
                {
                    return BadRequest(new { success = false, message = "Bạn đã ứng tuyển vị trí này rồi!" });
                }

                var don = new DonUngTuyen
                {
                    MaViTri = request.MaViTri,
                    MaCv = request.MaCv,
                    ThuGioiThieu = request.ThuGioiThieu,
                    NgayNop = DateTime.Now,
                    TrangThai = 0
                };

                _context.DonUngTuyens.Add(don);
                await _context.SaveChangesAsync();

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
        [HttpGet("/api/PhuongXa")]
        public async Task<IActionResult> GetPhuongXaByThanhPho([FromQuery] int maTP)
        {
            try
            {
                var phuongXas = await _context.PhuongXas
                    .Where(p => p.MaTp == maTP)
                    .Select(p => new {
                        maPhuong = p.MaPhuong,
                        tenPhuong = p.TenPhuong
                    })
                    .ToListAsync();

                if (!phuongXas.Any())
                {
                    return Ok(new { success = true, data = new object[] { } });
                }

                return Ok(new { success = true, data = phuongXas });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 7: POST /api/jobs/{maTin}/view (Cộng lượt xem tin tuyển dụng)
        // =================================================================
        [HttpPost("{maTin}/view")]
        public async Task<IActionResult> RecordJobView(int maTin)
        {
            try
            {
                var job = await _context.TinTuyenDungs.FirstOrDefaultAsync(j => j.MaTin == maTin);
                if (job == null) return NotFound(new { success = false, message = "Không tìm thấy tin tuyển dụng!" });

                job.LuotXem += 1;

                var viewLog = new LichSuXemTin
                {
                    MaTin = maTin,
                    ThoiGianXem = DateTime.Now
                };
                _context.LichSuXemTins.Add(viewLog);

                await _context.SaveChangesAsync();
                return Ok(new { success = true, currentViews = job.LuotXem });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 8: GET /api/jobs/bookmarked (Lấy danh sách mã vị trí đã lưu của User)
        // =================================================================
        [HttpGet("bookmarked")]
        public async Task<IActionResult> GetBookmarkedJobs([FromHeader] int maUser)
        {
            try
            {
                var bookmarkedIds = await _context.TinDaLuus
                    .Where(x => x.MaUser == maUser)
                    .Select(x => x.MaViTri)
                    .ToListAsync();

                return Ok(new { success = true, data = bookmarkedIds });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 9: GET /api/jobs/suggestions (Gợi ý từ khóa dựa trên Lịch sử nộp đơn & Thả tim)
        // =================================================================
        [HttpGet("suggestions")]
        public async Task<IActionResult> GetJobSuggestions([FromHeader] int? maUser)
        {
            try
            {
                var suggestions = new List<string>();

                if (maUser.HasValue && maUser.Value > 0)
                {
                    var appliedTitles = await _context.DonUngTuyens
                        .Include(d => d.MaViTriNavigation)
                        .Where(d => d.MaCvNavigation.MaUser == maUser.Value)
                        .OrderByDescending(d => d.NgayNop)
                        .Select(d => d.MaViTriNavigation.TenViTri)
                        .Take(3)
                        .ToListAsync();

                    suggestions.AddRange(appliedTitles);

                    if (suggestions.Count < 4)
                    {
                        var bookmarkedTitles = await _context.TinDaLuus
                            .Include(t => t.MaViTriNavigation)
                            .Where(t => t.MaUser == maUser.Value)
                            .OrderByDescending(t => t.NgayLuu)
                            .Select(t => t.MaViTriNavigation.TenViTri)
                            .Take(4 - suggestions.Count)
                            .ToListAsync();

                        suggestions.AddRange(bookmarkedTitles);
                    }
                }

                if (suggestions.Count < 4)
                {
                    var popularTitles = await _context.ChiTietViTris
                        .OrderByDescending(v => v.DonUngTuyens.Count)
                        .Select(v => v.TenViTri)
                        .Distinct()
                        .Take(4 - suggestions.Count)
                        .ToListAsync();

                    suggestions.AddRange(popularTitles);
                }

                return Ok(new { success = true, data = suggestions.Distinct().ToList() });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}