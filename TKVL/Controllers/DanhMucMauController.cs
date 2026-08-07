using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DanhMucMauController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public DanhMucMauController(JobPortalDbContext context)
        {
            _context = context;
        }

        // GET: Lấy danh sách danh mục
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _context.DanhMucMaus.ToListAsync();
            return Ok(data);
        }

        // POST: Thêm mới danh mục (Sử dụng [FromBody] nhận JSON)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DanhMucMauRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TenDanhMuc))
                return BadRequest(new { message = "Tên danh mục không được để trống!" });

            var entity = new DanhMucMau { TenDanhMuc = request.TenDanhMuc.Trim() };

            _context.DanhMucMaus.Add(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Thêm danh mục thành công!", data = entity });
        }

        // PUT: Cập nhật danh mục (Sử dụng [FromBody] nhận JSON)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] DanhMucMauRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TenDanhMuc))
                return BadRequest(new { message = "Tên danh mục không được để trống!" });

            var entity = await _context.DanhMucMaus.FindAsync(id);
            if (entity == null) return NotFound(new { message = "Không tìm thấy danh mục!" });

            entity.TenDanhMuc = request.TenDanhMuc.Trim();
            await _context.SaveChangesAsync();

            return Ok(new { message = "Cập nhật thành công!", data = entity });
        }

        // DELETE: Xóa danh mục
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _context.DanhMucMaus.FindAsync(id);
            if (entity == null) return NotFound(new { message = "Không tìm thấy danh mục!" });

            _context.DanhMucMaus.Remove(entity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Xóa danh mục thành công!" });
        }
    }

    public class DanhMucMauRequest
    {
        public string TenDanhMuc { get; set; }
    }
}