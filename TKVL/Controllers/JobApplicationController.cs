using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class JobApplicationController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public JobApplicationController(JobPortalDbContext context)
        {
            _context = context;
        }

        [HttpGet("my-applications")]
        public async Task<IActionResult> GetMyAppliedJobs()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được người dùng." });
                }

                var data = await _context.DonUngTuyens
                    .Include(d => d.MaCvNavigation)
                    .Include(d => d.MaViTriNavigation)
                        .ThenInclude(v => v.MaTinNavigation)
                            .ThenInclude(t => t.MaCongTyNavigation)
                    .Where(d => d.MaCvNavigation.MaUser == userId)
                    .OrderByDescending(d => d.NgayNop)
                    .Select(d => new
                    {
                        maDon = d.MaDon,
                        tenViTri = d.MaViTriNavigation.TenViTri,
                        tenCongTy = d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.TenCongTy,
                        luong = d.MaViTriNavigation.Luong,
                        ngayNop = d.NgayNop,
                        trangThai = d.TrangThai,
                        tieuDeCV = d.MaCvNavigation.TieuDe
                    })
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
