using Microsoft.AspNetCore.Authorization;
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
    public class SubscriptionCleanupController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public SubscriptionCleanupController(JobPortalDbContext context)
        {
            _context = context;
        }

        // ===================================================================
        // 1. API QUÉT VÀ RESET TẤT CẢ USER HẾT HẠN GÓI VỀ NULL
        // ===================================================================
        [HttpPost("process-expired-users")]
        public async Task<IActionResult> ProcessExpiredSubscriptions()
        {
            try
            {
                var now = DateTime.Now;

                // Lấy tất cả user có ngày hết hạn nhưng đã quá hạn
                var expiredUsers = await _context.Users
                    .Where(u => u.NgayHetHanGoi != null && u.NgayHetHanGoi < now)
                    .ToListAsync();

                if (!expiredUsers.Any())
                {
                    return Ok(new
                    {
                        success = true,
                        count = 0,
                        message = "Không có tài khoản nào hết hạn gói dịch vụ."
                    });
                }

                foreach (var user in expiredUsers)
                {
                    user.NgayHetHanGoi = null;
                    user.LuotXemCvConLai = 0; // Đặt lại lượt xem CV về 0 khi hết hạn gói
                }

                // Dọn dẹp các đặc quyền (UserDacQuyen) đã hết hạn tương ứng
                var expiredUserIds = expiredUsers.Select(u => u.MaUser).ToList();
                var expiredDacQuyens = await _context.UserDacQuyens
                    .Where(ud => expiredUserIds.Contains(ud.MaUser) && ud.NgayHetHan < now)
                    .ToListAsync();

                if (expiredDacQuyens.Any())
                {
                    _context.UserDacQuyens.RemoveRange(expiredDacQuyens);
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    count = expiredUsers.Count,
                    message = $"Đã reset thành công {expiredUsers.Count} tài khoản hết hạn gói về NULL."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi hệ thống khi quét và reset gói hết hạn.",
                    error = ex.Message
                });
            }
        }

        // ===================================================================
        // 2. API KIỂM TRA VÀ RESET GÓI CHO MỘT USER CỤ THỂ
        // ===================================================================
        [HttpPost("check-user/{userId}")]
        public async Task<IActionResult> CheckAndResetUserSubscription(int userId)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng." });
                }

                var now = DateTime.Now;
                if (user.NgayHetHanGoi != null && user.NgayHetHanGoi < now)
                {
                    user.NgayHetHanGoi = null;
                    user.LuotXemCvConLai = 0;

                    var expiredDacQuyens = await _context.UserDacQuyens
                        .Where(ud => ud.MaUser == userId && ud.NgayHetHan < now)
                        .ToListAsync();

                    if (expiredDacQuyens.Any())
                    {
                        _context.UserDacQuyens.RemoveRange(expiredDacQuyens);
                    }

                    await _context.SaveChangesAsync();

                    return Ok(new
                    {
                        success = true,
                        isExpired = true,
                        message = "Gói dịch vụ đã hết hạn và đã được đặt lại về NULL."
                    });
                }

                return Ok(new
                {
                    success = true,
                    isExpired = false,
                    ngayHetHanGoi = user.NgayHetHanGoi,
                    luotXemCvConLai = user.LuotXemCvConLai,
                    message = "Gói dịch vụ vẫn còn hiệu lực hoặc chưa kích hoạt."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi khi kiểm tra gói người dùng.",
                    error = ex.Message
                });
            }
        }

        // ===================================================================
        // 3. API THỐNG KÊ SỐ LƯỢNG TÀI KHOẢN ĐANG HẾT HẠN
        // ===================================================================
        [HttpGet("expired-count")]
        public async Task<IActionResult> GetExpiredCount()
        {
            try
            {
                var count = await _context.Users
                    .CountAsync(u => u.NgayHetHanGoi != null && u.NgayHetHanGoi < DateTime.Now);

                return Ok(new
                {
                    success = true,
                    expiredCount = count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = ex.Message
                });
            }
        }
    }
}