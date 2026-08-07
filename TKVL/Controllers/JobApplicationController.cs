using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class JobApplicationController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IEmailService _emailService;

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

        [HttpPost("remind-employer/{maDon}")]
        [Authorize] // Yêu cầu đăng nhập
        public async Task<IActionResult> RemindEmployer(int maDon)
        {
            // 1. Lấy maUser từ Token
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid");
            if (userIdClaim == null) return Unauthorized();
            int currentUserId = int.Parse(userIdClaim.Value);

            // 2. Tìm đơn ứng tuyển kèm thông tin Tin tuyển dụng, Công ty và Email NTD
            var application = await _context.DonUngTuyens
                .Include(d => d.MaCvNavigation)
                .Include(d => d.MaViTriNavigation)
                    .ThenInclude(v => v.MaTinNavigation)
                        .ThenInclude(t => t.MaCongTyNavigation)
                            .ThenInclude(c => c.MaUserNavigation)
                .FirstOrDefaultAsync(d => d.MaDon == maDon);

            if (application == null)
                return NotFound(new { message = "Không tìm thấy đơn ứng tuyển!" });

            // 3. Kiểm tra xem có đúng là ứng viên này nộp đơn không
            if (application.MaCvNavigation.MaUser != currentUserId)
                return Forbid();

            // 4. Kiểm tra logic 7 ngày ở Backend
            var daysDiff = (DateTime.Now - application.NgayNop).TotalDays;
            if (daysDiff < 7)
            {
                return BadRequest(new { message = "Chỉ có thể nhắc nhở sau 7 ngày kể từ lúc nộp hồ sơ!" });
            }

            // 5. Gửi Email nhắc nhở tới Nhà tuyển dụng
            try
            {
                var employerEmail = application.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.MaUserNavigation.Email;
                var jobTitle = application.MaViTriNavigation.TenViTri;
                var applicantName = User.Identity?.Name ?? "Một ứng viên";

                string emailSubject = $"[JobsNow] Lời nhắc phản hồi hồ sơ vị trí {jobTitle}";
                string emailBody = $@"
            <h3>Xin chào Nhà tuyển dụng,</h3>
            <p>Ứng viên <b>{applicantName}</b> đã nộp hồ sơ vào vị trí <b>{jobTitle}</b> vào ngày {application.NgayNop:dd/MM/yyyy}.</p>
            <p>Ứng viên vừa gửi một lời nhắc lịch sự mong muốn nhận được thông tin phản hồi từ quý công ty.</p>
            <p>Vui lòng đăng nhập hệ thống JobsNow để duyệt và cập nhật trạng thái hồ sơ.</p>";

                await _emailService.SendEmailAsync(employerEmail, emailSubject, emailBody);

                return Ok(new { message = "Đã gửi email nhắc nhở thành công tới Nhà tuyển dụng!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Không thể gửi email nhắc nhở lúc này.", error = ex.Message });
            }
        }
    }
}
