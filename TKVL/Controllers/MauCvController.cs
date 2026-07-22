using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TKVL.Models;
using TKVL.Dtos;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MauCvController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public MauCvController(JobPortalDbContext context)
        {
            _context = context;
        }

        // 1. API lấy danh sách mẫu CV (Ẩn dữ liệu mẫu để tối ưu hiệu năng)
        [HttpGet]
        public async Task<IActionResult> GetDanhSachMauCv([FromQuery] string? ngonNgu = null)
        {
            var query = _context.MauCVs.Where(m => m.TrangThai == true);

            if (!string.IsNullOrEmpty(ngonNgu))
            {
                query = query.Where(m => m.NgonNgu == ngonNgu);
            }

            // Bước A: Tải dữ liệu thô và chuỗi DanhSachMau gọn gàng từ SQL Server về RAM
            var templatesRaw = await query
                .Select(m => new
                {
                    Id = m.MaMau,
                    Title = m.TenMau,
                    Description = m.MoTa,
                    Image = m.AnhThumbnail,
                    IsATS = m.IsATS,
                    NgonNgu = m.NgonNgu,
                    Tags = m.Tags,
                    DanhSachMau = m.DanhSachMau, // 👈 Lấy chuỗi mã màu phẳng mới gộp
                    Categories = m.PhanLoaiMaus.Select(p => p.DanhMucMauNavigation.TenDanhMuc).ToList()
                })
                .ToListAsync();

            // Bước B: Cắt chuỗi dấu phẩy thành List<string> trên RAM để trả về đúng DTO cho React đọc
            var templates = templatesRaw.Select(m => new MauCvDto
            {
                Id = m.Id,
                Title = m.Title,
                Description = m.Description,
                Image = m.Image,
                IsATS = m.IsATS,
                NgonNgu = m.NgonNgu,
                Tags = m.Tags,
                Colors = !string.IsNullOrEmpty(m.DanhSachMau) ? m.DanhSachMau.Split(',').ToList() : new List<string>(),
                Categories = m.Categories,
                DuLieuMau = null // Không load ở trang danh sách
            }).ToList();

            return Ok(templates);
        }

        // 2. API lấy chi tiết mẫu CV (Bơm dữ liệu mẫu JSON để bắt đầu dựng CV)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMauCvById(int id)
        {
            // Bước A: Tải dữ liệu thô của mẫu CV cụ thể theo Id
            var templateRaw = await _context.MauCVs
                .Where(m => m.MaMau == id && m.TrangThai == true)
                .Select(m => new
                {
                    Id = m.MaMau,
                    Title = m.TenMau,
                    Description = m.MoTa,
                    Image = m.AnhThumbnail,
                    IsATS = m.IsATS,
                    NgonNgu = m.NgonNgu,
                    Tags = m.Tags,
                    DanhSachMau = m.DanhSachMau, // 👈 Lấy chuỗi mã màu phẳng mới gộp
                    Categories = m.PhanLoaiMaus.Select(p => p.DanhMucMauNavigation.TenDanhMuc).ToList(),
                    DuLieuMau = m.DuLieuMau,
                    LayoutJson = m.LayoutJson
                })
                .FirstOrDefaultAsync();

            if (templateRaw == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy mẫu CV này!" });
            }

            // Bước B: Map sang DTO và bẻ chuỗi ngăn cách bằng dấu phẩy thành mảng Colors
            var template = new MauCvDto
            {
                Id = templateRaw.Id,
                Title = templateRaw.Title,
                Description = templateRaw.Description,
                Image = templateRaw.Image,
                IsATS = templateRaw.IsATS,
                NgonNgu = templateRaw.NgonNgu,
                Tags = templateRaw.Tags,
                Colors = !string.IsNullOrEmpty(templateRaw.DanhSachMau) ? templateRaw.DanhSachMau.Split(',').ToList() : new List<string>(),
                Categories = templateRaw.Categories,
                DuLieuMau = templateRaw.DuLieuMau,
                LayoutJson = templateRaw.LayoutJson
            };

            return Ok(template);
        }
    }
}