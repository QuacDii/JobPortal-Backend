using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
    [Authorize]
    public class JobApplicationController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IEmailService _emailService;

        // 🌟 1. ĐÃ INJECT ĐẦY ĐỦ IEmailService VÀO CONSTRUCTOR
        public JobApplicationController(JobPortalDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // =========================================================================
        // 1. LẤY DANH SÁCH VIỆC LÀM ĐÃ ỨNG TUYỂN CỦA ỨNG VIÊN
        // =========================================================================
        [HttpGet("my-applications")]
        public async Task<IActionResult> GetMyAppliedJobs()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                               ?? User.FindFirst("nameid")?.Value
                               ?? User.FindFirst("sub")?.Value;

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
                        maViTri = d.MaViTri,
                        // 🌟 2. BẮT BUỘC TRẢ VỀ maTin ĐỂ FRONTEND CHUYỂN TRANG
                        maTin = d.MaViTriNavigation != null ? d.MaViTriNavigation.MaTin : 0,
                        tenViTri = d.MaViTriNavigation != null ? d.MaViTriNavigation.TenViTri : "Vị trí tuyển dụng",
                        tenCongTy = (d.MaViTriNavigation != null && d.MaViTriNavigation.MaTinNavigation != null && d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation != null)
                                    ? d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.TenCongTy : "Công ty ẩn danh",
                        logo = (d.MaViTriNavigation != null && d.MaViTriNavigation.MaTinNavigation != null && d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation != null)
                                    ? d.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.Logo : null,
                        luong = d.MaViTriNavigation != null ? d.MaViTriNavigation.Luong : "Thỏa thuận",
                        ngayNop = d.NgayNop,
                        trangThai = d.TrangThai,
                        maCv = d.MaCv,
                        tieuDeCV = d.MaCvNavigation != null ? d.MaCvNavigation.TieuDe : "Hồ sơ của tôi"
                    })
                    .ToListAsync();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        // =========================================================================
        // 2. GỬI EMAIL NHẮC NHỞ NHÀ TUYỂN DỤNG
        // =========================================================================
        [HttpPost("remind-employer/{maDon}")]
        public async Task<IActionResult> RemindEmployer(int maDon)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst("nameid")?.Value
                           ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized();
            }

            var application = await _context.DonUngTuyens
                .Include(d => d.MaCvNavigation)
                .Include(d => d.MaViTriNavigation)
                    .ThenInclude(v => v.MaTinNavigation)
                        .ThenInclude(t => t.MaCongTyNavigation)
                            .ThenInclude(c => c.MaUserNavigation)
                .FirstOrDefaultAsync(d => d.MaDon == maDon);

            if (application == null)
                return NotFound(new { message = "Không tìm thấy đơn ứng tuyển!" });

            if (application.MaCvNavigation.MaUser != currentUserId)
                return Forbid();

            var daysDiff = (DateTime.Now - application.NgayNop).TotalDays;
            if (daysDiff < 7)
            {
                return BadRequest(new { message = "Chỉ có thể nhắc nhở sau 7 ngày kể từ lúc nộp hồ sơ!" });
            }

            try
            {
                var employerEmail = application.MaViTriNavigation?.MaTinNavigation?.MaCongTyNavigation?.MaUserNavigation?.Email;
                if (string.IsNullOrEmpty(employerEmail))
                {
                    return BadRequest(new { message = "Nhà tuyển dụng chưa cập nhật email nhận tin!" });
                }

                var jobTitle = application.MaViTriNavigation?.TenViTri ?? "Vị trí tuyển dụng";
                var applicantName = application.MaCvNavigation?.TieuDe ?? "Ứng viên";

                string emailSubject = $"[JobsNow] Lời nhắc phản hồi hồ sơ ứng tuyển vị trí {jobTitle}";
                string emailBody = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; line-height: 1.6; color: #333;'>
                        <h3 style='color: #1890ff;'>Xin chào Nhà tuyển dụng,</h3>
                        <p>Ứng viên đã nộp hồ sơ vào vị trí <b>{jobTitle}</b> vào ngày <b>{application.NgayNop:dd/MM/yyyy}</b>.</p>
                        <p>Ứng viên vừa gửi một lời nhắc lịch sự mong muốn nhận được thông tin phản hồi từ quý công ty.</p>
                        <p>Vui lòng đăng nhập hệ thống JobsNow để duyệt và cập nhật trạng thái hồ sơ.</p>
                        <hr style='border: none; border-top: 1px solid #eee; margin: 20px 0;'/>
                        <p style='font-size: 12px; color: #888;'>Hệ thống Tuyển dụng JobsNow</p>
                    </div>";

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