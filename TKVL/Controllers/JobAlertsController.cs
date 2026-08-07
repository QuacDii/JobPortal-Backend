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
    public class JobAlertsController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public JobAlertsController(JobPortalDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // CÁC DTO (DATA TRANSFER OBJECTS)
        // ==========================================
        public class CreateJobAlertDto
        {
            public int MaUser { get; set; }
            public int MaNganh { get; set; }
            public string? TuKhoaKyNang { get; set; }
            public bool TrangThai { get; set; }
        }

        // DTO dùng cho hàm PUT (Bật/Tắt)
        public class ToggleDto
        {
            public bool TrangThai { get; set; }
        }


        // ==========================================
        // CÁC API ENDPOINTS
        // ==========================================

        // 1. LẤY DANH SÁCH JOB ALERT CỦA 1 USER
        // GET: api/JobAlerts/user/{userId}
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserAlerts(int userId)
        {
            try
            {
                var alerts = await _context.JobAlerts
                    .Where(a => a.MaUser == userId)
                    .OrderByDescending(a => a.MaAlert) // Xếp thông báo mới tạo lên đầu
                    .ToListAsync();

                return Ok(alerts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy dữ liệu!", error = ex.Message });
            }
        }

        // 2. THÊM MỚI JOB ALERT
        // POST: api/JobAlerts
        [HttpPost]
        public async Task<IActionResult> CreateAlert([FromBody] CreateJobAlertDto dto)
        {
            try
            {
                // 1. Kiểm tra trùng lặp
                var exists = await _context.JobAlerts.AnyAsync(a => a.MaUser == dto.MaUser && a.MaNganh == dto.MaNganh);
                if (exists) return BadRequest(new { message = "Bạn đã cài đặt thông báo cho ngành nghề này rồi!" });

                // 2. Tạo đối tượng mới
                var alert = new JobAlert
                {
                    MaUser = dto.MaUser,
                    MaNganh = dto.MaNganh,
                    TuKhoaKyNang = dto.TuKhoaKyNang,
                    TrangThai = dto.TrangThai
                };

                // 3. Đưa vào trạng thái chuẩn bị lưu
                _context.JobAlerts.Add(alert);

                // Gán giá trị trực tiếp cho cột ẩn (Shadow Property) dưới DB
                try
                {
                    _context.Entry(alert).Property("MaNganhNavigationMaNganh").CurrentValue = dto.MaNganh;
                }
                catch
                {
                }

                // 4. Lưu xuống Database
                await _context.SaveChangesAsync();

                return Ok(alert);
            }
            catch (Exception ex)
            {
                // Bắt lỗi chi tiết nhất từ Entity Framework
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return BadRequest(new { message = errorMessage });
            }
        }

        // 3. BẬT/TẮT JOB ALERT
        // PUT: api/JobAlerts/toggle/{id}
        [HttpPut("toggle/{id}")]
        public async Task<IActionResult> ToggleAlert(int id, [FromBody] ToggleDto dto)
        {
            try
            {
                var alert = await _context.JobAlerts.FindAsync(id);
                if (alert == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông báo cần sửa!" });
                }

                // Cập nhật trạng thái
                alert.TrangThai = dto.TrangThai;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Cập nhật trạng thái thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật!", error = ex.Message });
            }
        }

        // 4. XÓA JOB ALERT
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlert(int id)
        {
            try
            {
                var alert = await _context.JobAlerts.FindAsync(id);
                if (alert == null)
                {
                    return NotFound(new { message = "Không tìm thấy thông báo cần xóa!" });
                }

                _context.JobAlerts.Remove(alert);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Xóa thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi xóa!", error = ex.Message });
            }
        }
    }
}