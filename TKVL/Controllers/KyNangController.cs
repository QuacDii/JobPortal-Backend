using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class KyNangController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public KyNangController(JobPortalDbContext context)
        {
            _context = context;
        }

        // 1. API DÀNH CHO USER: Lấy danh sách kỹ năng đã được Admin DUYỆT để gợi ý (Auto-complete)
        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions([FromQuery] string? query)
        {
            var q = _context.KyNangs.Where(k => k.TrangThai == true);

            if (!string.IsNullOrWhiteSpace(query))
            {
                string cleanQuery = query.Trim().ToLower();
                q = q.Where(k => k.TenKyNang.ToLower().Contains(cleanQuery));
            }

            var suggestions = await q
                .OrderBy(k => k.TenKyNang)
                .Select(k => new { k.MaKyNang, k.TenKyNang })
                .Take(20)
                .ToListAsync();

            return Ok(suggestions);
        }

        // 2. API DÀNH CHO ADMIN: Lấy danh sách kỹ năng kèm bộ lọc
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] bool? status)
        {
            var q = _context.KyNangs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string cleanSearch = search.Trim().ToLower();
                q = q.Where(k => k.TenKyNang.ToLower().Contains(cleanSearch));
            }

            if (status.HasValue)
            {
                q = q.Where(k => k.TrangThai == status.Value);
            }

            var list = await q.OrderByDescending(k => k.MaKyNang).ToListAsync();
            return Ok(list);
        }

        // 3. TẠO MỚI / CHUẨN HÓA KHI TẠO (Chống tạo trùng tên)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] KyNang request)
        {
            if (string.IsNullOrWhiteSpace(request.TenKyNang))
                return BadRequest(new { message = "Tên kỹ năng không được để trống!" });

            string cleanName = request.TenKyNang.Trim();

            // Kiểm tra xem đã tồn tại kỹ năng nào trùng tên (không phân biệt hoa thường)
            var existing = await _context.KyNangs
                .FirstOrDefaultAsync(k => k.TenKyNang.ToLower() == cleanName.ToLower());

            if (existing != null)
            {
                return Ok(existing); // Trả về kỹ năng đã có sẵn thay vì tạo rác
            }

            var newSkill = new KyNang
            {
                TenKyNang = cleanName,
                TrangThai = request.TrangThai ?? true // Mặc định Admin tạo là true
            };

            _context.KyNangs.Add(newSkill);
            await _context.SaveChangesAsync();
            return Ok(newSkill);
        }

        // 4. BẬT / TẮT TRẠNG THÁI DUYỆT
        [HttpPatch("{id}/toggle-status")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var item = await _context.KyNangs.FindAsync(id);
            if (item == null) return NotFound(new { message = "Không tìm thấy kỹ năng!" });

            item.TrangThai = !(item.TrangThai ?? false);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, trangThai = item.TrangThai, message = "Đã cập nhật trạng thái!" });
        }

        // 5. CẬP NHẬT TÊN KỸ NĂNG
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] KyNang request)
        {
            var item = await _context.KyNangs.FindAsync(id);
            if (item == null) return NotFound();

            item.TenKyNang = request.TenKyNang.Trim();
            if (request.TrangThai.HasValue) item.TrangThai = request.TrangThai;

            await _context.SaveChangesAsync();
            return Ok(item);
        }

        // 6. XÓA KỸ NĂNG
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.KyNangs.FindAsync(id);
            if (item == null) return NotFound();

            _context.KyNangs.Remove(item);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 7. TÍNH NĂNG GỘP KỸ NĂNG TRÙNG
        [HttpPost("merge")]
        public async Task<IActionResult> MergeSkills([FromBody] MergeSkillsDto dto)
        {
            if (dto.SourceIds == null || !dto.SourceIds.Any() || dto.TargetId <= 0)
            {
                return BadRequest(new { message = "Dữ liệu gộp không hợp lệ!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var targetSkill = await _context.KyNangs.FindAsync(dto.TargetId);
                if (targetSkill == null) return NotFound(new { message = "Kỹ năng đích không tồn tại!" });

                // Lấy tất cả các kỹ năng cần gộp (loại trừ ID đích)
                var sourceIds = dto.SourceIds.Where(id => id != dto.TargetId).ToList();
                var sourceSkills = await _context.KyNangs
                    .Include(k => k.MaViTris)
                    .Where(k => sourceIds.Contains(k.MaKyNang))
                    .ToListAsync();

                foreach (var source in sourceSkills)
                {
                    // Chuyển toàn bộ liên kết vị trí tuyển dụng sang Kỹ năng đích
                    foreach (var viTri in source.MaViTris.ToList())
                    {
                        if (!targetSkill.MaViTris.Contains(viTri))
                        {
                            targetSkill.MaViTris.Add(viTri);
                        }
                    }
                    _context.KyNangs.Remove(source);
                }

                targetSkill.TrangThai = true; // Tự động duyệt kỹ năng đích sau khi gộp
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = $"Đã gộp thành công {sourceSkills.Count} kỹ năng vào '{targetSkill.TenKyNang}'!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi khi thực hiện gộp kỹ năng!", error = ex.Message });
            }
        }
        // 8. TÍNH NĂNG XÓA HÀNG LOẠT (DỌN RÁC)
        [HttpPost("bulk-delete")]
        public async Task<IActionResult> BulkDelete([FromBody] List<int> ids)
        {
            if (ids == null || !ids.Any())
            {
                return BadRequest(new { message = "Danh sách kỹ năng cần xóa trống!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var skills = await _context.KyNangs
                    .Include(k => k.MaViTris)
                    .Where(k => ids.Contains(k.MaKyNang))
                    .ToListAsync();

                foreach (var skill in skills)
                {
                    // Tự động gỡ các liên kết vị trí tuyển dụng trước khi xóa
                    skill.MaViTris.Clear();
                    _context.KyNangs.Remove(skill);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = $"Đã dọn dẹp thành công {skills.Count} kỹ năng rác!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi khi xóa hàng loạt kỹ năng!", error = ex.Message });
            }
        }
    }

    public class MergeSkillsDto
    {
        public int TargetId { get; set; }
        public List<int> SourceIds { get; set; } = new();
    }
}