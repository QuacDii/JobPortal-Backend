using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NganhNgheController : ControllerBase
    {
        private readonly JobPortalDbContext _context; 

        public NganhNgheController(JobPortalDbContext context)
        {
            _context = context;
        }

        [HttpGet("danh-sach")]
        public async Task<IActionResult> GetDanhSachNganhNghe()
        {
            // Lấy danh sách ngành nghề đang hoạt động
            var danhSach = await _context.NganhNghes
                .Where(n => n.TrangThai == true)
                .Select(n => new
                {
                    maNganh = n.MaNganh,
                    tenNganh = n.TenNganh
                })
                .ToListAsync();

            return Ok(danhSach);
        }
    }
}
