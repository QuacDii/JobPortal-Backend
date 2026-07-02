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

            var templates = await query
                .Select(m => new MauCvDto
                {
                    Id = m.MaMau,
                    Title = m.TenMau,
                    Description = m.MoTa,
                    Image = m.AnhThumbnail,
                    IsATS = m.IsATS,
                    NgonNgu = m.NgonNgu,
                    Tags = m.Tags,
                    Colors = m.MauSacs.Select(c => c.MaHex).ToList(),
                    Categories = m.PhanLoaiMaus.Select(p => p.DanhMucMauNavigation.TenDanhMuc).ToList(),

                    DuLieuMau = null // Không load ở trang danh sách
                })
                .ToListAsync();

            return Ok(templates);
        }

        // 2. API lấy chi tiết mẫu CV (Bơm dữ liệu mẫu JSON để bắt đầu dựng CV)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMauCvById(int id)
        {
            var template = await _context.MauCVs
                .Where(m => m.MaMau == id && m.TrangThai == true)
                .Select(m => new MauCvDto
                {
                    Id = m.MaMau,
                    Title = m.TenMau,
                    Description = m.MoTa,
                    Image = m.AnhThumbnail,
                    IsATS = m.IsATS,
                    NgonNgu = m.NgonNgu,
                    Tags = m.Tags,
                    Colors = m.MauSacs.Select(c => c.MaHex).ToList(),
                    Categories = m.PhanLoaiMaus.Select(p => p.DanhMucMauNavigation.TenDanhMuc).ToList(),

                    DuLieuMau = m.DuLieuMau // Trả về chuỗi JSON chứa nội dung mẫu
                })
                .FirstOrDefaultAsync();

            if (template == null)
            {
                return NotFound(new { success = false, message = "Không tìm thấy mẫu CV này!" });
            }

            return Ok(template);
        }
    }
}