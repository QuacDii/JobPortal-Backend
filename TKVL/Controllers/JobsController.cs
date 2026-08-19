using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public JobsController(JobPortalDbContext context)
        {
            _context = context;
        }

        // =================================================================
        // API 1: GET /api/jobs (Lấy tin việc làm hiển thị lên Trang Chủ)
        // =================================================================
        [HttpGet]
        public async Task<IActionResult> GetHomeJobs()
        {
            try
            {
                var now = DateTime.Now;

                var campaigns = await _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation)
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(c => c.MaPhuongNavigation)
                            .ThenInclude(p => p.MaTpNavigation)
                    // 1. Chiến dịch cha phải đang hoạt động (TrangThai = 1) và còn hạn[cite: 9]
                    .Where(t => t.TrangThai == 1 && t.NgayHetHan >= now)
                    // 2. Phải có ít nhất 1 vị trí con được duyệt (TrangThai = 1) và còn hạn[cite: 9]
                    .Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now)))
                    // 3. Ưu tiên tin VIP lên đầu[cite: 9]
                    .OrderByDescending(t => t.IsPromoted)
                    .ThenByDescending(t => t.MaTin)
                    .Select(t => new
                    {
                        maTin = t.MaTin,
                        tieuDeChienDich = t.TieuDeChienDich,
                        companyName = t.MaCongTyNavigation != null ? t.MaCongTyNavigation.TenCongTy : "Công ty ẩn danh",
                        logo = t.MaCongTyNavigation != null ? t.MaCongTyNavigation.Logo : null,
                        deadline = t.NgayHetHan,
                        isPromoted = t.IsPromoted,
                        // CHỈ LẤY CÁC VỊ TRÍ CON HỢP LỆ[cite: 9]
                        viTris = t.ChiTietViTris
                            .Where(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now))
                            .Select(c => new
                            {
                                id = c.MaViTri,
                                title = c.TenViTri,
                                capBac = c.CapBac,
                                kinhNghiem = c.KinhNghiem,
                                salaryRange = c.Luong,
                                deadline = c.NgayHetHan,
                                locationName = c.MaPhuongNavigation != null && c.MaPhuongNavigation.MaTpNavigation != null
                                    ? c.MaPhuongNavigation.MaTpNavigation.TenTp
                                    : "Toàn quốc"
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
        // API 2: GET /api/jobs/search (BỘ LỌC NÂNG CAO ĐA CHIỀU)
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
            [FromQuery] string? capBac,
            [FromQuery] string? mucLuong
        )
        {
            try
            {
                var now = DateTime.Now;

                // 🌟 1. BẮT ĐẦU TỪ VỊ TRÍ CON (ChiTietViTris) ĐỂ LỌC CHÍNH XÁC TỪNG VỊ TRÍ
                var viTriQuery = _context.ChiTietViTris
                    .Include(c => c.MaTinNavigation)
                        .ThenInclude(t => t.MaCongTyNavigation)
                    .Include(c => c.MaPhuongNavigation)
                        .ThenInclude(p => p.MaTpNavigation)
                    .Where(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now))
                    .Where(c => c.MaTinNavigation.TrangThai == 1 && c.MaTinNavigation.NgayHetHan >= now)
                    .AsQueryable();

                // Lọc Keyword
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string kw = keyword.Trim().ToLower();
                    viTriQuery = viTriQuery.Where(c =>
                        c.TenViTri.ToLower().Contains(kw) ||
                        c.MaTinNavigation.TieuDeChienDich.ToLower().Contains(kw) ||
                        (c.MaTinNavigation.MaCongTyNavigation != null && c.MaTinNavigation.MaCongTyNavigation.TenCongTy.ToLower().Contains(kw)));
                }

                // Lọc Địa điểm
                if (maPhuong.HasValue && maPhuong.Value > 0)
                {
                    viTriQuery = viTriQuery.Where(c => c.MaPhuong == maPhuong.Value);
                }
                else if (maTP.HasValue && maTP.Value > 0)
                {
                    viTriQuery = viTriQuery.Where(c => c.MaPhuongNavigation != null && c.MaPhuongNavigation.MaTp == maTP.Value);
                }

                // 🌟 Lọc Ngành nghề (Chỉ giữ lại đúng vị trí thuộc ngành được chọn)
                if (!string.IsNullOrWhiteSpace(maNganh) && maNganh != "all")
                {
                    var nganhIds = maNganh
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => int.TryParse(x.Trim(), out int id) ? id : (int?)null)
                        .Where(x => x.HasValue)
                        .Select(x => x.Value)
                        .ToList();

                    if (nganhIds.Count > 0)
                    {
                        var allRelatedNganhConIds = await _context.NganhNgheCons
                            .Where(n => nganhIds.Contains(n.MaNganhCon) || nganhIds.Contains(n.MaNganhCha))
                            .Select(n => n.MaNganhCon)
                            .ToListAsync();

                        viTriQuery = viTriQuery.Where(c => nganhIds.Contains(c.MaNganhCon) || allRelatedNganhConIds.Contains(c.MaNganhCon));
                    }
                }

                // Lọc Pro Company (VIP)
                if (isPromoted.HasValue && isPromoted.Value)
                {
                    viTriQuery = viTriQuery.Where(c => c.MaTinNavigation.IsPromoted == true);
                }

                // Lọc Cấp bậc
                if (!string.IsNullOrWhiteSpace(capBac) && capBac != "all" && capBac != "Tất cả")
                {
                    var capBacList = capBac.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLower()).ToList();
                    if (capBacList.Count > 0)
                    {
                        viTriQuery = viTriQuery.Where(c => c.CapBac != null && capBacList.Any(cb => c.CapBac.ToLower().Trim() == cb));
                    }
                }

                // Lọc Kinh nghiệm
                if (!string.IsNullOrWhiteSpace(kinhNghiem) && kinhNghiem != "all" && kinhNghiem != "Tất cả")
                {
                    var knList = kinhNghiem.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim().ToLower()).ToList();
                    if (knList.Count > 0)
                    {
                        viTriQuery = viTriQuery.Where(c => c.KinhNghiem != null && knList.Any(kn => c.KinhNghiem.ToLower().Contains(kn)));
                    }
                }

                // Lọc Mức lương
                if (!string.IsNullOrWhiteSpace(mucLuongRadio) && mucLuongRadio != "all")
                {
                    switch (mucLuongRadio)
                    {
                        case "thoa-thuan":
                            viTriQuery = viTriQuery.Where(c => c.Luong != null && (c.Luong.ToLower().Contains("thỏa thuận") || c.Luong.ToLower().Contains("thoa thuan")));
                            break;
                        case "duoi-10": tuLuong = 0; denLuong = 10; break;
                        case "10-15": tuLuong = 10; denLuong = 15; break;
                        case "15-20": tuLuong = 15; denLuong = 20; break;
                        case "20-25": tuLuong = 20; denLuong = 25; break;
                        case "25-30": tuLuong = 25; denLuong = 30; break;
                        case "30-50": tuLuong = 30; denLuong = 50; break;
                        case "tren-50": tuLuong = 50; denLuong = null; break;
                    }
                }

                if (tuLuong.HasValue || denLuong.HasValue)
                {
                    viTriQuery = viTriQuery.Where(c => c.Luong != null && !c.Luong.ToLower().Contains("thỏa thuận") && !c.Luong.ToLower().Contains("thoa thuan"));

                    if (tuLuong.HasValue && !denLuong.HasValue)
                    {
                        viTriQuery = viTriQuery.Where(c => c.Luong.Contains(tuLuong.Value.ToString()) || c.Luong.ToLower().Contains("trên " + tuLuong.Value));
                    }
                    else if (tuLuong.HasValue && denLuong.HasValue)
                    {
                        viTriQuery = viTriQuery.Where(c => c.Luong.Contains(tuLuong.Value.ToString()) || c.Luong.Contains(denLuong.Value.ToString()));
                    }
                }
                else if (!string.IsNullOrWhiteSpace(mucLuong) && mucLuong != "all" && mucLuong != "Tất cả")
                {
                    viTriQuery = viTriQuery.Where(c => c.Luong != null && c.Luong.Contains(mucLuong));
                }

                // 🌟 2. LẤY DANH SÁCH VỊ TRÍ ĐÃ LỌC
                var rawPositions = await viTriQuery.ToListAsync();

                // 🌟 3. GỘP NHÓM THEO CHIẾN DỊCH CHA 
                var results = rawPositions
                    .GroupBy(c => c.MaTinNavigation)
                    .OrderByDescending(g => g.Key.IsPromoted)
                    .ThenByDescending(g => g.Key.NgayHetHan)
                    .Select(g => new
                    {
                        maTin = g.Key.MaTin,
                        tieuDeChienDich = g.Key.TieuDeChienDich,
                        companyName = g.Key.MaCongTyNavigation != null ? g.Key.MaCongTyNavigation.TenCongTy : "Công ty ẩn danh",
                        logo = g.Key.MaCongTyNavigation != null ? g.Key.MaCongTyNavigation.Logo : null,
                        deadline = g.Key.NgayHetHan,
                        isPromoted = g.Key.IsPromoted,
                        viTris = g.Select(c => new
                        {
                            id = c.MaViTri,
                            title = c.TenViTri,
                            capBac = c.CapBac,
                            kinhNghiem = c.KinhNghiem,
                            salaryRange = c.Luong,
                            deadline = c.NgayHetHan,
                            locationName = c.MaPhuongNavigation != null && c.MaPhuongNavigation.MaTpNavigation != null
                                ? c.MaPhuongNavigation.MaTpNavigation.TenTp
                                : "Toàn quốc"
                        }).ToList()
                    })
                    .ToList();

                return Ok(new { success = true, data = results });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 3: GET /api/jobs/{id} (Chi tiết chiến dịch phía Ứng viên)
        // =================================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetJobDetail(int id)
        {
            var now = DateTime.Now;

            var jobDetail = await _context.TinTuyenDungs
                .Include(t => t.MaCongTyNavigation)
                .Include(t => t.ChiTietViTris)
                    .ThenInclude(c => c.MaPhuongNavigation)
                        .ThenInclude(p => p.MaTpNavigation)
                .Where(t => t.MaTin == id && t.TrangThai == 1 && t.NgayHetHan >= now)
                .Select(t => new
                {
                    id = t.MaTin,
                    title = t.TieuDeChienDich,
                    companyName = t.MaCongTyNavigation != null ? t.MaCongTyNavigation.TenCongTy : "Công ty ẩn danh",
                    logo = t.MaCongTyNavigation != null ? t.MaCongTyNavigation.Logo : null,
                    deadline = t.NgayHetHan,
                    // Chỉ trả về các vị trí đã duyệt (TrangThai = 1) và còn hạn
                    danhSachViTri = t.ChiTietViTris
                        .Where(v => v.TrangThai == 1 && (!v.NgayHetHan.HasValue || v.NgayHetHan >= now))
                        .Select(v => new
                        {
                            maViTri = v.MaViTri,
                            tenViTri = v.TenViTri,
                            luong = v.Luong,
                            soLuongTuyen = v.SoLuongTuyen,
                            moTaCongViec = v.MoTaCongViec,
                            yeuCauUngVien = v.YeuCauUngVien,
                            quyenLoi = v.QuyenLoi,
                            capBac = v.CapBac,
                            ngayHetHan = v.NgayHetHan,
                            phuongXa = v.MaPhuongNavigation != null ? v.MaPhuongNavigation.TenPhuong : "",
                            locationName = v.MaPhuongNavigation != null && v.MaPhuongNavigation.MaTpNavigation != null
                                ? v.MaPhuongNavigation.MaTpNavigation.TenTp
                                : "Toàn quốc"
                        }).ToList()
                })
                .FirstOrDefaultAsync();

            if (jobDetail == null || !jobDetail.danhSachViTri.Any())
            {
                return NotFound(new { success = false, message = "Chiến dịch tuyển dụng không tồn tại hoặc đã hết hạn." });
            }

            return Ok(new { success = true, data = jobDetail });
        }

        // =================================================================
        // API 4: POST /api/jobs/{id}/bookmark
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

        // DTO Nộp đơn[cite: 9]
        public class ApplyRequest
        {
            public int MaViTri { get; set; }
            public int MaCv { get; set; }
            public string ThuGioiThieu { get; set; } = string.Empty;
        }

        // =================================================================
        // API 5: POST /api/jobs/{id}/apply (Nộp Đơn Ứng Tuyển)
        // =================================================================
        [HttpPost("{id}/apply")]
        public async Task<IActionResult> ApplyJob(int id, [FromBody] ApplyRequest request)
        {
            try
            {
                var now = DateTime.Now;

                // 1. Kiểm tra vị trí ứng tuyển có đang mở và còn hạn hay không[cite: 9]
                var position = await _context.ChiTietViTris
                    .Include(v => v.MaTinNavigation)
                    .FirstOrDefaultAsync(v => v.MaViTri == request.MaViTri
                                           && v.TrangThai == 1
                                           && (!v.NgayHetHan.HasValue || v.NgayHetHan >= now)
                                           && v.MaTinNavigation.TrangThai == 1
                                           && v.MaTinNavigation.NgayHetHan >= now);

                if (position == null)
                {
                    return BadRequest(new { success = false, message = "Vị trí tuyển dụng này đã đóng hoặc đã hết hạn nộp hồ sơ!" });
                }

                // 2. Xác định User ID từ CV[cite: 9]
                var cv = await _context.Cvs.FindAsync(request.MaCv);
                if (cv == null)
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy hồ sơ CV trong hệ thống!" });
                }

                int currentUserId = cv.MaUser;

                // 3. Kiểm tra xem ứng viên đã nộp vào vị trí NÀY chưa[cite: 9]
                var alreadyApplied = await _context.DonUngTuyens
                    .AnyAsync(d => d.MaViTri == request.MaViTri && d.MaCvNavigation.MaUser == currentUserId);

                if (alreadyApplied)
                {
                    return BadRequest(new { success = false, message = "Bạn đã ứng tuyển vị trí này rồi!" });
                }

                // 4. Tạo đơn ứng tuyển[cite: 9]
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
        // API 6: GET /api/PhuongXa
        // =================================================================
        [HttpGet("/api/PhuongXa")]
        public async Task<IActionResult> GetPhuongXaByThanhPho([FromQuery] int maTP)
        {
            try
            {
                var phuongXas = await _context.PhuongXas
                    .Where(p => p.MaTp == maTP)
                    .Select(p => new
                    {
                        maPhuong = p.MaPhuong,
                        tenPhuong = p.TenPhuong
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = phuongXas });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 7: POST /api/jobs/{maTin}/view
        // =================================================================
        [HttpPost("{maTin}/view")]
        public async Task<IActionResult> RecordJobView(int maTin)
        {
            try
            {
                var job = await _context.TinTuyenDungs.FirstOrDefaultAsync(j => j.MaTin == maTin);
                if (job == null) return NotFound(new { success = false, message = "Không tìm thấy tin tuyển dụng!" });

                job.LuotXem += 1;
                _context.LichSuXemTins.Add(new LichSuXemTin
                {
                    MaTin = maTin,
                    ThoiGianXem = DateTime.Now
                });

                await _context.SaveChangesAsync();
                return Ok(new { success = true, currentViews = job.LuotXem });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 8: GET /api/jobs/bookmarked
        // =================================================================
        [HttpGet("bookmarked")]
        public async Task<IActionResult> GetBookmarkedJobs([FromHeader] int maUser)
        {
            try
            {
                var bookmarks = await _context.TinDaLuus
                    .Include(t => t.MaViTriNavigation)
                    .Include(t => t.MaViTriNavigation)
                    .Where(x => x.MaUser == maUser)
                    .Select(x => new
                    {
                        maViTri = x.MaViTri,
                        maTin = x.MaViTriNavigation != null ? x.MaViTriNavigation.MaTin : 0
                    })
                    .ToListAsync();

                var viTriIds = bookmarks.Select(b => b.maViTri).Distinct().ToList();
                var tinIds = bookmarks.Select(b => b.maTin).Where(id => id > 0).Distinct().ToList();

                return Ok(new
                {
                    success = true,
                    data = viTriIds, // Tương thích ngược
                    viTriIds = viTriIds,
                    maTinIds = tinIds
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 9: GET /api/jobs/suggestions
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
                        .Where(v => v.TrangThai == 1)
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