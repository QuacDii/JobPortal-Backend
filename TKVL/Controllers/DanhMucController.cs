using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DanhMucController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public DanhMucController(JobPortalDbContext context)
        {
            _context = context;
        }

        // =================================================================
        // API 1: GET /api/ThanhPho (Lấy toàn bộ danh sách Tỉnh/Thành phố)
        // =================================================================
        [HttpGet("/api/ThanhPho")]
        public async Task<IActionResult> GetThanhPhos()
        {
            try
            {
                var data = await _context.ThanhPhos
                    .Select(tp => new
                    {
                        maTP = tp.MaTp,
                        tenTP = tp.TenTp
                    })
                    .OrderBy(tp => tp.tenTP) // Sắp xếp theo bảng chữ cái cho đẹp
                    .ToListAsync();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =================================================================
        // API 2: GET /api/NganhNghe (Lấy toàn bộ danh sách Ngành nghề)
        // =================================================================
        [HttpGet("/api/NganhNghe")]
        public async Task<IActionResult> GetNganhNghes()
        {
            try
            {
                var data = await _context.NganhNghes
                    .Where(nn => nn.TrangThai == true) 
                    .Select(nn => new
                    {
                        maNganh = nn.MaNganh,
                        tenNganh = nn.TenNganh
                    })
                    .OrderBy(nn => nn.tenNganh)
                    .ToListAsync();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
