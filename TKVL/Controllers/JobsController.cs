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
                    // 1. Chiến dịch cha phải đang hoạt động (TrangThai = 1) và còn hạn
                    .Where(t => t.TrangThai == 1 && t.NgayHetHan >= now)
                    // 2. Phải có ít nhất 1 vị trí con được duyệt (TrangThai = 1) và còn hạn
                    .Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now)))
                    // 3. Ưu tiên tin VIP lên đầu
                    .OrderByDescending(t => t.IsPromoted)
                    .ThenByDescending(t => t.MaTin)
                    .Select(t => new
                    {
                        maTin = t.MaTin,
                        tieuDeChienDich = t.TieuDeChienDich,
                        companyName = t.MaCongTyNavigation.TenCongTy,
                        logo = t.MaCongTyNavigation.Logo,
                        deadline = t.NgayHetHan,
                        isPromoted = t.IsPromoted,
                        // CHỈ LẤY CÁC VỊ TRÍ CON HỢP LỆ
                        viTris = t.ChiTietViTris
                            .Where(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now))
                            .Select(c => new
                            {
                                id = c.MaViTri,
                                title = c.TenViTri,
                                capBac = c.CapBac,
                                salaryRange = c.Luong,
                                deadline = c.NgayHetHan,
                                locationName = c.MaPhuongNavigation.MaTpNavigation.TenTp
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
         [FromQuery] int? maNganh,
         [FromQuery] string? capBac,
         [FromQuery] string? mucLuong,
         [FromQuery] string? kinhNghiem 
 )
        {
            try
            {
                var now = DateTime.Now;

                var query = _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation)
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(c => c.MaPhuongNavigation)
                            .ThenInclude(p => p.MaTpNavigation)
                    .Where(t => t.TrangThai == 1 && t.NgayHetHan >= now)
                    .Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now)))
                    .AsQueryable();

                // 1. Lọc Keyword
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string kw = keyword.Trim().ToLower();
                    query = query.Where(t => t.TieuDeChienDich.ToLower().Contains(kw) ||
                                             t.MaCongTyNavigation.TenCongTy.ToLower().Contains(kw) ||
                                             t.ChiTietViTris.Any(c => c.TrangThai == 1 &&
                                                                      (!c.NgayHetHan.HasValue || c.NgayHetHan >= now) &&
                                                                      c.TenViTri.ToLower().Contains(kw)));
                }

                // 2. Lọc Địa điểm
                if (maPhuong.HasValue && maPhuong.Value > 0)
                {
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now) && c.MaPhuong == maPhuong.Value));
                }
                else if (maTP.HasValue && maTP.Value > 0)
                {
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now) && c.MaPhuongNavigation.MaTp == maTP.Value));
                }

                // 3. Lọc Ngành nghề
                if (maNganh.HasValue && maNganh.Value > 0)
                {
                    var allRelatedNganhConIds = await _context.NganhNgheCons
                        .Where(n => n.MaNganhCon == maNganh.Value || n.MaNganhCha == maNganh.Value)
                        .Select(n => n.MaNganhCon)
                        .ToListAsync();

                    query = query.Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now) && allRelatedNganhConIds.Contains(c.MaNganhCon)));
                }

                // 4. Lọc Cấp bậc
                if (!string.IsNullOrEmpty(capBac) && capBac != "Tất cả")
                {
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now) && c.CapBac != null && c.CapBac.Contains(capBac)));
                }

                // 5. Lọc Mức lương
                if (!string.IsNullOrEmpty(mucLuong) && mucLuong != "Tất cả")
                {
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now) && c.Luong != null && c.Luong.Contains(mucLuong)));
                }

                // 🌟 6. Lọc Kinh nghiệm
                if (!string.IsNullOrWhiteSpace(kinhNghiem) && kinhNghiem != "Tất cả")
                {
                    query = query.Where(t => t.ChiTietViTris.Any(c => c.TrangThai == 1 &&
                                                                      (!c.NgayHetHan.HasValue || c.NgayHetHan >= now) &&
                                                                      c.KinhNghiem == kinhNghiem));
                }

                var results = await query
                    .OrderByDescending(t => t.IsPromoted)
                    .ThenByDescending(t => t.NgayHetHan)
                    .Select(t => new
                    {
                        maTin = t.MaTin,
                        tieuDeChienDich = t.TieuDeChienDich,
                        companyName = t.MaCongTyNavigation.TenCongTy,
                        logo = t.MaCongTyNavigation.Logo,
                        deadline = t.NgayHetHan,
                        isPromoted = t.IsPromoted,
                        viTris = t.ChiTietViTris
                            .Where(c => c.TrangThai == 1 && (!c.NgayHetHan.HasValue || c.NgayHetHan >= now))
                            .Select(c => new
                            {
                                id = c.MaViTri,
                                title = c.TenViTri,
                                capBac = c.CapBac,
                                kinhNghiem = c.KinhNghiem, // 🌟 Trả về kinh nghiệm
                                salaryRange = c.Luong,
                                deadline = c.NgayHetHan,
                                locationName = c.MaPhuongNavigation.MaTpNavigation.TenTp
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
                    companyName = t.MaCongTyNavigation.TenCongTy,
                    logo = t.MaCongTyNavigation.Logo,
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
                            phuongXa = v.MaPhuongNavigation.TenPhuong,
                            locationName = v.MaPhuongNavigation.MaTpNavigation.TenTp
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

        // DTO Nộp đơn
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

                // 1. Kiểm tra vị trí ứng tuyển có đang mở và còn hạn hay không
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

                // 2. Xác định User ID từ CV
                var cv = await _context.Cvs.FindAsync(request.MaCv);
                if (cv == null)
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy hồ sơ CV trong hệ thống!" });
                }

                int currentUserId = cv.MaUser;

                // 3. Kiểm tra xem ứng viên đã nộp vào vị trí NÀY chưa
                var alreadyApplied = await _context.DonUngTuyens
                    .AnyAsync(d => d.MaViTri == request.MaViTri && d.MaCvNavigation.MaUser == currentUserId);

                if (alreadyApplied)
                {
                    return BadRequest(new { success = false, message = "Bạn đã ứng tuyển vị trí này rồi!" });
                }

                // 4. Tạo đơn ứng tuyển
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