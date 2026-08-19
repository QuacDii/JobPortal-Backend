using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobAlertsController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IEmailService _emailService; 

        public JobAlertsController(JobPortalDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // ==========================================
        // CÁC DTO (DATA TRANSFER OBJECTS)
        // ==========================================
        public class CreateJobAlertDto
        {
            public int MaUser { get; set; }
            public int MaNganhCon { get; set; }
            public string? TuKhoaKyNang { get; set; }
            public bool TrangThai { get; set; }
        }

        public class ToggleDto
        {
            public bool TrangThai { get; set; }
        }

        // ==========================================
        // CÁC API ENDPOINTS
        // ==========================================

        // 1. LẤY DANH SÁCH JOB ALERT CỦA 1 USER
        [HttpGet("user/{userId}")]
        public async Task<IActionResult> GetUserAlerts(int userId)
        {
            try
            {
                var alerts = await _context.JobAlerts
                    .Include(a => a.MaNganhConNavigation)
                    .Where(a => a.MaUser == userId)
                    .OrderByDescending(a => a.MaAlert)
                    .Select(a => new
                    {
                        a.MaAlert,
                        a.MaUser,
                        a.MaNganhCon,
                        tenNganhCon = a.MaNganhConNavigation != null ? a.MaNganhConNavigation.TenNganhCon : null,
                        a.TuKhoaKyNang,
                        a.TrangThai
                    })
                    .ToListAsync();

                return Ok(alerts);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy dữ liệu!", error = ex.Message });
            }
        }

        // 2. TẠO MỚI JOB ALERT
        [HttpPost]
        public async Task<IActionResult> CreateAlert([FromBody] CreateJobAlertDto dto)
        {
            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var user = await _context.Users.FindAsync(userId);

                if (user == null || !user.IsEmailVerified)
                {
                    return BadRequest(new { message = "Vui lòng xác thực email của bạn trước khi cài đặt Job Alerts!" });
                }
                var exists = await _context.JobAlerts.AnyAsync(a => a.MaUser == dto.MaUser && a.MaNganhCon == dto.MaNganhCon);
                if (exists) return BadRequest(new { message = "Bạn đã cài đặt thông báo cho ngành nghề này rồi!" });

                var alert = new JobAlert
                {
                    MaUser = dto.MaUser,
                    MaNganhCon = dto.MaNganhCon,
                    TuKhoaKyNang = dto.TuKhoaKyNang,
                    TrangThai = dto.TrangThai
                };

                _context.JobAlerts.Add(alert);
                await _context.SaveChangesAsync();

                return Ok(alert);
            }
            catch (Exception ex)
            {
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return BadRequest(new { message = errorMessage });
            }
        }

        // 3. BẬT/TẮT JOB ALERT
        [HttpPut("toggle/{id}")]
        public async Task<IActionResult> ToggleAlert(int id, [FromBody] ToggleDto dto)
        {
            try
            {
                var alert = await _context.JobAlerts.FindAsync(id);
                if (alert == null)
                    return NotFound(new { message = "Không tìm thấy thông báo cần sửa!" });

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
                    return NotFound(new { message = "Không tìm thấy thông báo cần xóa!" });

                _context.JobAlerts.Remove(alert);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Xóa thành công" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi xóa!", error = ex.Message });
            }
        }

        // 🌟 5. API DEMO GỬI EMAIL TỨC THÌ
        // POST: api/JobAlerts/demo-send/{userId}
        [HttpPost("demo-send/{userId}")]
        public async Task<IActionResult> DemoSendJobAlert(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản người dùng!" });

                if (string.IsNullOrEmpty(user.Email) || user.Email.Contains("@facebook.com"))
                    return BadRequest(new { success = false, message = "Tài khoản chưa có email hợp lệ để nhận tin!" });

                // Lấy ngành nghề user đang theo dõi
                var userAlertIndustries = await _context.JobAlerts
                    .Where(a => a.MaUser == userId && a.TrangThai == true)
                    .Select(a => a.MaNganhCon)
                    .ToListAsync();

                // Lấy thêm ngành nghề user đã từng nộp CV
                var appliedIndustries = await (from don in _context.DonUngTuyens
                                               join cv in _context.Cvs on don.MaCv equals cv.MaCv
                                               join vt in _context.ChiTietViTris on don.MaViTri equals vt.MaViTri
                                               where cv.MaUser == userId
                                               select vt.MaNganhCon).Distinct().ToListAsync();

                var targetIndustries = userAlertIndustries.Concat(appliedIndustries).Distinct().ToList();

                if (!targetIndustries.Any())
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Bạn chưa có ngành nghề nào đang bật theo dõi hoặc chưa từng ứng tuyển công việc nào!"
                    });
                }

                // Lấy tối đa 5 việc làm còn hạn tuyển
                var matchingJobs = await (from vt in _context.ChiTietViTris
                                          join tin in _context.TinTuyenDungs on vt.MaTin equals tin.MaTin
                                          join ct in _context.CongTies on tin.MaCongTy equals ct.MaCongTy
                                          where targetIndustries.Contains(vt.MaNganhCon)
                                             && tin.TrangThai == 1
                                             && tin.NgayHetHan >= DateTime.Now
                                          orderby tin.NgayHetHan descending
                                          select new
                                          {
                                              vt.MaViTri,
                                              vt.TenViTri,
                                              vt.Luong,
                                              ct.TenCongTy,
                                              ct.Logo
                                          }).Take(5).ToListAsync();

                if (!matchingJobs.Any())
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Hiện không có tin tuyển dụng nào còn hạn phù hợp với các ngành nghề đang theo dõi."
                    });
                }

                // Soạn thảo nội dung Email
                string jobListHtml = "";
                foreach (var job in matchingJobs)
                {
                    jobListHtml += $@"
                    <div style='border: 1px solid #eee; padding: 15px; margin-bottom: 15px; border-radius: 8px; background-color: #fcfcfc;'>
                        <h3 style='color: #1890ff; margin: 0 0 5px 0; font-size: 16px;'>{job.TenViTri}</h3>
                        <p style='margin: 0; color: #555; font-size: 14px;'>🏢 <b>{job.TenCongTy}</b></p>
                        <p style='margin: 5px 0 0 0; color: #00b14f; font-weight: bold;'>💰 Lương: {job.Luong}</p>
                        <div style='margin-top: 10px;'>
                            <a href='http://localhost:5173/chi-tiet-viec-lam/{job.MaViTri}' style='display: inline-block; padding: 8px 15px; background-color: #1890ff; color: #ffffff; text-decoration: none; border-radius: 5px; font-size: 13px;'>Xem chi tiết & Ứng tuyển</a>
                        </div>
                    </div>";
                }

                string emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; padding: 20px; border-radius: 8px;'>
                    <div style='text-align: center; border-bottom: 2px solid #00b14f; padding-bottom: 15px; margin-bottom: 20px;'>
                        <h2 style='color: #00b14f; margin: 0;'>JOBSNOW TÌM VIỆC</h2>
                        <p style='color: #666; margin: 5px 0 0 0;'>[DEMO THỬ NGHIỆM] Bản tin Việc làm Phù hợp</p>
                    </div>
                    <p>Chào <b>{user.HoTen}</b>,</p>
                    <p>Hệ thống vừa tìm thấy <b>{matchingJobs.Count}</b> việc làm mới nhất phù hợp với danh sách ngành nghề bạn quan tâm:</p>
                    
                    {jobListHtml}

                    <p style='color: #8c8c8c; font-size: 13px; text-align: center; margin-top: 30px; border-top: 1px solid #e0e0e0; padding-top: 15px;'>
                        Email này được kích hoạt thông qua chế độ Demo thử nghiệm của hệ thống JOBSNOW.
                    </p>
                </div>";

                await _emailService.SendEmailAsync(user.Email, "[DEMO JOBSNOW] Thông báo việc làm mới nhất phù hợp với bạn", emailBody);

                return Ok(new
                {
                    success = true,
                    message = $"Đã kích hoạt gửi email thành công ({matchingJobs.Count} việc làm) tới {user.Email}!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi gửi email demo!", error = ex.Message });
            }
        }
    }
}