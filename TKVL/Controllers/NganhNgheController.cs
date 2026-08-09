using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
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

        // 1. Trả về cây danh mục phân cấp (Ngành cha -> Danh sách ngành con)
        [HttpGet("tree")]
        public async Task<IActionResult> GetNganhNgheTree()
        {
            try
            {
                var tree = await _context.NganhNgheChas
                    .Select(cha => new
                    {
                        maNganh = cha.MaNganhCha,
                        tenNganh = cha.TenNganhCha,
                        danhSachCon = cha.NganhNgheCons.Select(con => new
                        {
                            maNganh = con.MaNganhCon,
                            tenNganh = con.TenNganhCon,
                            maNganhCha = con.MaNganhCha
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = tree });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // 2. Lấy danh sách ngành con cho Frontend
        [HttpGet("danh-sach")]
        public async Task<IActionResult> GetDanhSachNganhNghe()
        {
            var danhSach = await _context.NganhNgheCons
                .Select(n => new
                {
                    maNganh = n.MaNganhCon,
                    tenNganh = n.TenNganhCon,
                    maNganhCha = n.MaNganhCha
                })
                .ToListAsync();

            return Ok(danhSach);
        }

        // 3. Lấy tất cả danh mục cho trang Quản trị Admin
        [HttpGet]
        public async Task<IActionResult> GetAllForAdmin()
        {
            var danhSach = await _context.NganhNgheChas
                .Include(c => c.NganhNgheCons)
                .Select(cha => new
                {
                    maNganhCha = cha.MaNganhCha,
                    tenNganhCha = cha.TenNganhCha,
                    nganhNgheCons = cha.NganhNgheCons.Select(con => new
                    {
                        maNganhCon = con.MaNganhCon,
                        tenNganhCon = con.TenNganhCon
                    }).ToList()
                })
                .ToListAsync();

            return Ok(danhSach);
        }

        // DTO nhận dữ liệu Tạo / Sửa ngành nghề
        public class NganhNgheRequestDto
        {
            public string TenNganh { get; set; } = string.Empty;
            public int? MaNganhCha { get; set; }
        }

        // 4. Tạo mới (Nếu truyền MaNganhCha sẽ tạo Ngành con, ngược lại tạo Ngành cha)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] NganhNgheRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.TenNganh))
            {
                return BadRequest(new { message = "Tên ngành nghề không được để trống!" });
            }

            if (request.MaNganhCha.HasValue && request.MaNganhCha.Value > 0)
            {
                var nganhCon = new NganhNgheCon
                {
                    TenNganhCon = request.TenNganh,
                    MaNganhCha = request.MaNganhCha.Value
                };
                _context.NganhNgheCons.Add(nganhCon);
                await _context.SaveChangesAsync();
                return Ok(nganhCon);
            }
            else
            {
                var nganhCha = new NganhNgheCha
                {
                    TenNganhCha = request.TenNganh
                };
                _context.NganhNgheChas.Add(nganhCha);
                await _context.SaveChangesAsync();
                return Ok(nganhCha);
            }
        }

        // 5. Cập nhật Ngành con theo ID
        [HttpPut("con/{id}")]
        public async Task<IActionResult> UpdateNganhCon(int id, [FromBody] NganhNgheRequestDto request)
        {
            var item = await _context.NganhNgheCons.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy ngành con!" });

            item.TenNganhCon = request.TenNganh;
            if (request.MaNganhCha.HasValue && request.MaNganhCha.Value > 0)
            {
                item.MaNganhCha = request.MaNganhCha.Value;
            }
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        // 6. Cập nhật Ngành cha theo ID
        [HttpPut("cha/{id}")]
        public async Task<IActionResult> UpdateNganhCha(int id, [FromBody] NganhNgheRequestDto request)
        {
            var item = await _context.NganhNgheChas.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy ngành cha!" });

            item.TenNganhCha = request.TenNganh;
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        // 7. Xóa Ngành con
        [HttpDelete("con/{id}")]
        public async Task<IActionResult> DeleteNganhCon(int id)
        {
            var item = await _context.NganhNgheCons.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dữ liệu!" });

            try
            {
                _context.NganhNgheCons.Remove(item);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { message = "Không thể xóa vì Ngành nghề này đang được sử dụng ở bài tuyển dụng/CV." });
            }
        }

        // 8. Xóa Ngành cha
        [HttpDelete("cha/{id}")]
        public async Task<IActionResult> DeleteNganhCha(int id)
        {
            var item = await _context.NganhNgheChas.Include(c => c.NganhNgheCons).FirstOrDefaultAsync(c => c.MaNganhCha == id);
            if (item == null) return NotFound(new { message = "Không tìm thấy dữ liệu!" });

            try
            {
                _context.NganhNgheChas.Remove(item);
                await _context.SaveChangesAsync();
                return Ok(new { success = true });
            }
            catch (DbUpdateException)
            {
                return BadRequest(new { message = "Không thể xóa vì Ngành cha này đang chứa các ngành con." });
            }
        }
    }
}