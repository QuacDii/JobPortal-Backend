using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ThanhPhoController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        public ThanhPhoController(JobPortalDbContext context)
        {
            _context = context;
        }

        // GET: api/ThanhPho
        [HttpGet]
        public async Task<IActionResult> GetThanhPho()
        {
            var danhSach = await _context.ThanhPhos.ToListAsync();
            return Ok(new { success = true, data = danhSach });
        }
    }
}
