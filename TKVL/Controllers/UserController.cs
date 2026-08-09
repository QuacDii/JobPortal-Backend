using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;
using TKVL.Dtos;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IServiceScopeFactory _scopeFactory;

        public UserController(
            JobPortalDbContext context,
            IEmailService emailService,
            IServiceScopeFactory scopeFactory)
        {
            _context = context;
            _emailService = emailService;
            _scopeFactory = scopeFactory;
        }

        // =========================================================
        // API 1: GET /api/User/profile/{id} (LẤY THÔNG TIN PROFILE)
        // =========================================================
        [HttpGet("profile/{id}")]
        public async Task<IActionResult> GetUserProfile(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng!" });

                return Ok(new
                {
                    success = true,
                    maUser = user.MaUser,
                    hoTen = user.HoTen ?? "",
                    email = user.Email ?? "",
                    isEmailVerified = user.IsEmailVerified == true,
                    trangThaiTimViec = user.TrangThaiTimViec != null && Convert.ToInt32(user.TrangThaiTimViec) == 1
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi xử lý Server!", error = ex.Message });
            }
        }

        // =========================================================
        // API 2: PUT /api/User/profile/{id} (CẬP NHẬT PROFILE)
        // =========================================================
        [HttpPut("profile/{id}")]
        public async Task<IActionResult> UpdateUserProfile(int id, [FromBody] UpdateProfileDto dto)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                    return NotFound(new { success = false, message = "Không tìm thấy người dùng!" });

                user.HoTen = dto.HoTen;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Cập nhật họ tên thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống!", error = ex.Message });
            }
        }

        // =========================================================
        // API 3: PUT /api/User/toggle-job-search/{id} (BẬT/TẮT TÌM VIỆC)
        // =========================================================
        [HttpPut("toggle-job-search/{id}")]
        public async Task<IActionResult> ToggleJobSearch(int id, [FromBody] ToggleJobSearchDto dto)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin tài khoản!" });
                }

                user.TrangThaiTimViec = dto.IsSearching;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                if (dto.IsSearching)
                {
                    string userEmail = user.Email;
                    string userHoTen = user.HoTen;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<JobPortalDbContext>();
                            var emailSvc = scope.ServiceProvider.GetRequiredService<IEmailService>();

                            var nganhIds = await db.JobAlerts
                                .Where(a => a.MaUser == id && a.TrangThai == true)
                                .Select(a => a.MaNganhCon)
                                .ToListAsync();

                            if (nganhIds.Any())
                            {
                                var matchingJobs = await (from vt in db.ChiTietViTris
                                                          join tin in db.TinTuyenDungs on vt.MaTin equals tin.MaTin
                                                          join ct in db.CongTies on tin.MaCongTy equals ct.MaCongTy
                                                          where nganhIds.Contains(vt.MaNganhCon)
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

                                if (matchingJobs.Any())
                                {
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
                                            <p style='color: #666; margin: 5px 0 0 0;'>Cơ hội tuyệt vời dành riêng cho bạn</p>
                                        </div>
                                        <p>Chào <b>{userHoTen}</b>,</p>
                                        <p>Chúc mừng bạn đã bật trạng thái tìm việc! Dựa vào các <b>Thông báo việc làm (Job Alerts)</b> bạn đã cài đặt, hệ thống JOBSNOW vừa tìm thấy một số vị trí cực kỳ phù hợp với bạn hiện nay:</p>
                                        
                                        {jobListHtml}

                                        <p style='color: #8c8c8c; font-size: 13px; text-align: center; margin-top: 30px; border-top: 1px solid #e0e0e0; padding-top: 15px;'>
                                            Hồ sơ của bạn hiện đang được ưu tiên hiển thị với Nhà tuyển dụng. Chúc bạn sớm tìm được công việc ưng ý!
                                        </p>
                                    </div>";

                                    await emailSvc.SendEmailAsync(userEmail, "[JOBSNOW] Việc làm mới nhất phù hợp với bạn!", emailBody);
                                }
                            }
                        }
                        catch (Exception emailEx)
                        {
                            Console.WriteLine($"[LỖI GỬI EMAIL GỢI Ý JOB]: {emailEx.Message}");
                        }
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = dto.IsSearching ? "Đã bật trạng thái tìm việc! Hồ sơ của bạn đã sẵn sàng." : "Đã ẩn hồ sơ khỏi Nhà tuyển dụng!",
                    isSearching = user.TrangThaiTimViec
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi cập nhật trạng thái!", error = ex.Message });
            }
        }

        // =========================================================
        // API 4: GET /api/User (LẤY DANH SÁCH TẤT CẢ USER FOR ADMIN)
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                var users = await _context.Users
                    .Select(u => new
                    {
                        u.MaUser,
                        u.HoTen,
                        u.Email,
                        u.VaiTro,
                        u.NgayTao,
                        u.TrangThai
                    })
                    .OrderByDescending(u => u.NgayTao)
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy dữ liệu người dùng", error = ex.Message });
            }
        }

        // =========================================================
        // API 5: PUT /api/User/toggle-status/{id} (KHÓA / MỞ KHÓA USER)
        // =========================================================
        [HttpPut("toggle-status/{id}")]
        public async Task<IActionResult> ToggleStatus(int id, [FromBody] ToggleStatusDto dto)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { message = "Không tìm thấy người dùng!" });
                }

                if (user.VaiTro == 0 && dto.TrangThai == false)
                {
                    return BadRequest(new { message = "Không thể khóa tài khoản của Quản trị viên!" });
                }

                user.TrangThai = dto.TrangThai;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Cập nhật trạng thái thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật trạng thái", error = ex.Message });
            }
        }

        // =========================================================
        // API 6: DELETE /api/User/{id} (XÓA TÀI KHOẢN VĨNH VIỄN)
        // =========================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy tài khoản người dùng!" });
                }

                if (user.VaiTro == 0)
                {
                    return BadRequest(new { success = false, message = "Không thể xóa tài khoản Quản trị viên!" });
                }

                var tinDaLuus = await _context.TinDaLuus.Where(t => t.MaUser == id).ToListAsync();
                _context.TinDaLuus.RemoveRange(tinDaLuus);

                var ungVienDaLuus = await _context.UngVienDaLuus.Where(u => u.MaUser == id).ToListAsync();
                _context.UngVienDaLuus.RemoveRange(ungVienDaLuus);

                var jobAlerts = await _context.JobAlerts.Where(j => j.MaUser == id).ToListAsync();
                _context.JobAlerts.RemoveRange(jobAlerts);

                var lichSuMoKhoas = await _context.LichSuMoKhoaCvs.Where(l => l.MaUser == id).ToListAsync();
                _context.LichSuMoKhoaCvs.RemoveRange(lichSuMoKhoas);

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Đã xóa vĩnh viễn tài khoản và giải phóng Email thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xóa tài khoản!", error = ex.Message });
            }
        }
    }

    // Các DTO hỗ trợ
    public class ToggleStatusDto
    {
        public bool TrangThai { get; set; }
    }

    public class UpdateProfileDto
    {
        public string? HoTen { get; set; }
    }
}