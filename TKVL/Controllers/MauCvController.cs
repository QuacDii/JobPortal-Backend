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

        [HttpGet]
        public async Task<IActionResult> GetDanhSachMauCv()
        {
            var templates = await _context.MauCVs
                .Where(m => m.TrangThai == true)
                .Select(m => new MauCvDto
                {
                    Id = m.MaMau,
                    Title = m.TenMau,
                    Description = m.MoTa,
                    Image = m.AnhThumbnail,
                    IsATS = m.IsATS,
                    // Dùng LINQ gom màu và danh mục thành mảng chuỗi
                    Colors = m.MauSacs.Select(c => c.MaHex).ToList(),
                    Categories = m.PhanLoaiMaus.Select(p => p.DanhMucMauNavigation.TenDanhMuc).ToList()
                })
                .ToListAsync();

            return Ok(templates);
        }
    }
}