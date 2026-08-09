using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TKVL.Dtos;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MauCvController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public MauCvController(JobPortalDbContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        [HttpGet("menu-data")]
        public async Task<IActionResult> GetMenuData()
        {
            try
            {
                // Lấy danh sách Style
                var styles = await _context.DanhMucMaus
                    .Select(d => new { id = d.MaDanhMuc, name = d.TenDanhMuc })
                    .ToListAsync();

                // Lấy Top Ngành nghề cha phổ biến dựa vào đếm số công việc thuộc các ngành nghề con
                var popularIndustries = await _context.NganhNgheChas
                    .Select(n => new {
                        id = n.MaNganhCha,
                        name = n.TenNganhCha,
                        jobCount = _context.ChiTietViTris.Count(v => _context.NganhNgheCons
                            .Where(c => c.MaNganhCha == n.MaNganhCha)
                            .Select(c => c.MaNganhCon)
                            .Contains(v.MaNganhCon))
                    })
                    .OrderByDescending(n => n.jobCount)
                    .Take(5)
                    .ToListAsync();

                return Ok(new { success = true, styles, popularIndustries });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi lấy dữ liệu menu CV", error = ex.Message });
            }
        }

        // 2. API LỌC MẪU CV THEO MÃ NGÀNH NGHỀ CHA
        [HttpGet]
        public async Task<IActionResult> GetDanhSachMauCv(
            [FromQuery] string? ngonNgu = null,
            [FromQuery] bool? activeOnly = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] int? industryId = null) 
        {
            var query = _context.MauCVs.AsQueryable();

            if (activeOnly != false)
            {
                query = query.Where(m => m.TrangThai);
            }

            if (!string.IsNullOrEmpty(ngonNgu))
            {
                query = query.Where(m => m.NgonNgu == ngonNgu);
            }

            if (categoryId.HasValue)
            {
                query = query.Where(m => m.PhanLoaiMaus.Any(p => p.MaDanhMuc == categoryId.Value));
            }

            // 🌟 Lọc mẫu CV theo Ngành nghề cha (Tìm tên ngành cha hoặc tên các ngành con thuộc ngành cha đó trong Tags)
            if (industryId.HasValue)
            {
                var nganhCha = await _context.NganhNgheChas.FindAsync(industryId.Value);
                if (nganhCha != null)
                {
                    // 1. Tách chuỗi tên ngành cha theo dấu '/' hoặc ',' thành danh sách từ khóa riêng lẻ
                    var keywords = nganhCha.TenNganhCha
                        .Split(new[] { '/', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(k => k.Trim())
                        .Where(k => !string.IsNullOrEmpty(k))
                        .ToList();

                    // 2. Lấy thêm tên các ngành nghề con thuộc ngành cha này
                    var tenNganhCons = await _context.NganhNgheCons
                        .Where(c => c.MaNganhCha == industryId.Value)
                        .Select(c => c.TenNganhCon)
                        .ToListAsync();

                    keywords.AddRange(tenNganhCons);
                    keywords = keywords.Distinct().ToList();

                    // 3. Tìm mẫu CV chứa BẤT KỲ từ khóa nào trong Tags, TenMau hoặc MoTa
                    query = query.Where(m => keywords.Any(kw =>
                        (m.Tags != null && m.Tags.Contains(kw)) ||
                        (m.TenMau != null && m.TenMau.Contains(kw)) ||
                        (m.MoTa != null && m.MoTa.Contains(kw))
                    ));
                }
            }

            var templates = await query
                .Select(m => new MauCvDto
                {
                    Id = m.MaMau,
                    MaMau = m.MaMau,
                    TenMau = m.TenMau,
                    Title = m.TenMau,
                    MoTa = m.MoTa,
                    AnhThumbnail = m.AnhThumbnail,
                    Image = m.AnhThumbnail,
                    IsATS = m.IsATS,
                    IsVip = m.IsVip,
                    TrangThai = m.TrangThai,
                    NgonNgu = m.NgonNgu,
                    Tags = m.Tags,
                    DanhSachMau = m.DanhSachMau,
                    Categories = m.PhanLoaiMaus.Select(p => p.DanhMucMauNavigation.TenDanhMuc).ToList(),
                    CategoryIds = m.PhanLoaiMaus.Select(p => p.MaDanhMuc).ToList(),
                    DuLieuMau = m.DuLieuMau,
                    LayoutJson = m.LayoutJson
                })
                .OrderByDescending(m => m.Id)
                .ToListAsync();

            return Ok(templates);
        }

        // GET: api/MauCv/categories
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.DanhMucMaus
                .Select(c => new { c.MaDanhMuc, c.TenDanhMuc })
                .ToListAsync();

            return Ok(categories);
        }

        // GET: api/MauCv/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMauCvById(int id)
        {
            var template = await _context.MauCVs
                .Where(m => m.MaMau == id)
                .Select(m => new MauCvDto
                {
                    Id = m.MaMau,
                    MaMau = m.MaMau,
                    TenMau = m.TenMau,
                    Title = m.TenMau,
                    MoTa = m.MoTa,
                    AnhThumbnail = m.AnhThumbnail,
                    Image = m.AnhThumbnail,
                    IsATS = m.IsATS,
                    IsVip = m.IsVip,
                    TrangThai = m.TrangThai,
                    NgonNgu = m.NgonNgu,
                    Tags = m.Tags,
                    DanhSachMau = m.DanhSachMau,
                    Categories = m.PhanLoaiMaus.Select(p => p.DanhMucMauNavigation.TenDanhMuc).ToList(),
                    CategoryIds = m.PhanLoaiMaus.Select(p => p.MaDanhMuc).ToList(),
                    DuLieuMau = m.DuLieuMau,
                    LayoutJson = m.LayoutJson
                })
                .FirstOrDefaultAsync();

            if (template == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy mẫu CV này!" });
            }

            return Ok(template);
        }

        // POST: api/MauCv
        [HttpPost]
        public async Task<IActionResult> CreateMauCv([FromForm] MauCvCreateRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                string? thumbnailUrl = request.AnhThumbnail;

                if (request.FileThumbnail != null)
                {
                    thumbnailUrl = await _cloudinaryService.UploadCvThumbnailAsync(request.FileThumbnail);
                }

                var entity = new MauCV
                {
                    TenMau = request.TenMau,
                    MoTa = request.MoTa,
                    AnhThumbnail = thumbnailUrl,
                    IsATS = request.IsATS,
                    IsVip = request.IsVip,
                    TrangThai = request.TrangThai,
                    NgonNgu = string.IsNullOrEmpty(request.NgonNgu) ? "VI" : request.NgonNgu,
                    Tags = request.Tags,
                    DuLieuMau = string.IsNullOrEmpty(request.DuLieuMau) ? "{}" : request.DuLieuMau,
                    LayoutJson = request.LayoutJson,
                    DanhSachMau = request.DanhSachMau
                };

                _context.MauCVs.Add(entity);
                await _context.SaveChangesAsync();

                if (request.CategoryIds != null && request.CategoryIds.Any())
                {
                    var phanLoais = request.CategoryIds.Select(cId => new PhanLoaiMau
                    {
                        MaMau = entity.MaMau,
                        MaDanhMuc = cId
                    });
                    _context.PhanLoaiMaus.AddRange(phanLoais);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return Ok(new { success = true, message = "Thêm mới mẫu CV thành công!", data = entity });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi thêm mẫu CV", error = ex.Message });
            }
        }

        // PUT: api/MauCv/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMauCv(int id, [FromForm] MauCvUpdateRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var entity = await _context.MauCVs.FindAsync(id);
                if (entity == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy mẫu CV!" });
                }

                if (request.FileThumbnail != null)
                {
                    entity.AnhThumbnail = await _cloudinaryService.UploadCvThumbnailAsync(request.FileThumbnail);
                }
                else if (!string.IsNullOrEmpty(request.AnhThumbnail))
                {
                    entity.AnhThumbnail = request.AnhThumbnail;
                }

                entity.TenMau = request.TenMau ?? entity.TenMau;
                entity.MoTa = request.MoTa ?? entity.MoTa;
                entity.IsATS = request.IsATS;
                entity.IsVip = request.IsVip;
                entity.TrangThai = request.TrangThai;
                entity.NgonNgu = request.NgonNgu ?? entity.NgonNgu;
                entity.Tags = request.Tags ?? entity.Tags;
                entity.DuLieuMau = request.DuLieuMau ?? entity.DuLieuMau;
                entity.LayoutJson = request.LayoutJson ?? entity.LayoutJson;
                entity.DanhSachMau = request.DanhSachMau ?? entity.DanhSachMau;

                if (request.CategoryIds != null)
                {
                    var oldCategories = _context.PhanLoaiMaus.Where(p => p.MaMau == id);
                    _context.PhanLoaiMaus.RemoveRange(oldCategories);

                    var newCategories = request.CategoryIds.Select(cId => new PhanLoaiMau
                    {
                        MaMau = id,
                        MaDanhMuc = cId
                    });
                    _context.PhanLoaiMaus.AddRange(newCategories);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Cập nhật mẫu CV thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi cập nhật", error = ex.Message });
            }
        }

        // PUT: api/MauCv/5/toggle-status
        [HttpPut("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(int id, [FromBody] ToggleStatusRequest req)
        {
            var entity = await _context.MauCVs.FindAsync(id);
            if (entity == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy mẫu CV!" });
            }

            entity.TrangThai = req.TrangThai;
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã cập nhật trạng thái hiển thị!" });
        }

        // DELETE: api/MauCv/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMauCv(int id)
        {
            var entity = await _context.MauCVs.FindAsync(id);
            if (entity == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy mẫu CV!" });
            }

            _context.MauCVs.Remove(entity);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Đã xóa mẫu CV thành công!" });
        }
    }
}