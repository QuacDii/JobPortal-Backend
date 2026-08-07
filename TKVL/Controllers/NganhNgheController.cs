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

        // Trả về cây danh mục phân cấp nhiều tầng cho Frontend
        [HttpGet("tree")]
        public async Task<IActionResult> GetNganhNgheTree()
        {
            var allNganh = await _context.NganhNghes.Where(n => n.TrangThai).ToListAsync();

            var tree = allNganh.Where(n => n.MaNganhCha == null)
                .Select(parent => new
                {
                    maNganh = parent.MaNganh,
                    tenNganh = parent.TenNganh,
                    danhSachCon = allNganh.Where(c => c.MaNganhCha == parent.MaNganh).Select(child => new
                    {
                        maNganh = child.MaNganh,
                        tenNganh = child.TenNganh,
                        viTriChuyenMon = allNganh.Where(sub => sub.MaNganhCha == child.MaNganh).Select(sub => new
                        {
                            maNganh = sub.MaNganh,
                            tenNganh = sub.TenNganh
                        }).ToList()
                    }).ToList()
                }).ToList();

            return Ok(new { success = true, data = tree });
        }

        [HttpGet("danh-sach")]
        public async Task<IActionResult> GetDanhSachNganhNghe()
        {
            var danhSach = await _context.NganhNghes
                .Where(n => n.TrangThai == true)
                .Select(n => new
                {
                    maNganh = n.MaNganh,
                    tenNganh = n.TenNganh,
                    maNganhCha = n.MaNganhCha
                })
                .ToListAsync();

            return Ok(danhSach);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllForAdmin()
        {
            var danhSach = await _context.NganhNghes
                .Include(n => n.NganhCha)
                .ToListAsync();
            return Ok(danhSach);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NganhNghe request)
        {
            request.TrangThai = true;
            _context.NganhNghes.Add(request);
            await _context.SaveChangesAsync();
            return Ok(request);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] NganhNghe request)
        {
            var item = await _context.NganhNghes.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dữ liệu!" });

            item.TenNganh = request.TenNganh;
            item.MaNganhCha = request.MaNganhCha; 
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.NganhNghes.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dữ liệu!" });

            try
            {
                _context.NganhNghes.Remove(item);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { message = "Không thể xóa vì Ngành nghề này đang có ngành con hoặc đang được sử dụng ở bài tuyển dụng/CV." });
            }
        }
    }
}