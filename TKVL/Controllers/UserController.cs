using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

        public UserController(JobPortalDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet("profile/{id}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            return Ok(new
            {
                HoTen = user.HoTen,
                Email = user.Email,
                TrangThaiTimViec = user.TrangThaiTimViec
            });
        }

        [HttpPut("toggle-job-search/{id}")]
        public async Task<IActionResult> ToggleJobSearch(int id, [FromBody] ToggleJobSearchDto dto)
        {
            try
            {
                // 1. Tìm user
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin tài khoản!" });
                }

                // 2. Cập nhật trạng thái
                user.TrangThaiTimViec = dto.IsSearching;
                _context.Users.Update(user);
                await _context.SaveChangesAsync();

                // =========================================================
                // 3. LOGIC GỬI EMAIL GỢI Ý CÔNG VIỆC (CHỈ CHẠY KHI BẬT)
                // =========================================================
                if (dto.IsSearching)
                {
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Tạo scope mới vì đang chạy thread ẩn (Fire-and-forget)
                            using var scope = HttpContext.RequestServices.CreateScope();
                            var db = scope.ServiceProvider.GetRequiredService<JobPortalDbContext>();
                            var emailSvc = scope.ServiceProvider.GetRequiredService<IEmailService>();

                            // a. Lấy các ngành nghề mà User đã cài đặt trong JobAlerts
                            var nganhIds = await db.JobAlerts
                                .Where(a => a.MaUser == id && a.TrangThai == true)
                                .Select(a => a.MaNganh)
                                .ToListAsync();

                            if (nganhIds.Any())
                            {
                                // b. Tìm 5 công việc phù hợp nhất (Cùng ngành, Còn hạn, Đang mở)
                                var matchingJobs = await (from vt in db.ChiTietViTris
                                                          join tin in db.TinTuyenDungs on vt.MaTin equals tin.MaTin
                                                          join ct in db.CongTies on tin.MaTin equals ct.MaCongTy
                                                          where nganhIds.Contains(vt.MaNganh)
                                                             && tin.TrangThai == 1 // Trạng thái đang tuyển
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

                                // c. Nếu có công việc phù hợp -> Soạn HTML và gửi mail
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
                                        <p>Chào <b>{user.HoTen}</b>,</p>
                                        <p>Chúc mừng bạn đã bật trạng thái tìm việc! Dựa vào các <b>Thông báo việc làm (Job Alerts)</b> bạn đã cài đặt, hệ thống JOBSNOW vừa tìm thấy một số vị trí cực kỳ phù hợp với bạn hiện nay:</p>
                                        
                                        {jobListHtml}

                                        <p style='color: #8c8c8c; font-size: 13px; text-align: center; margin-top: 30px; border-top: 1px solid #e0e0e0; padding-top: 15px;'>
                                            Hồ sơ của bạn hiện đang được ưu tiên hiển thị với Nhà tuyển dụng. Chúc bạn sớm tìm được công việc ưng ý!
                                        </p>
                                    </div>";

                                    await emailSvc.SendEmailAsync(user.Email, "[JOBSNOW] Việc làm mới nhất phù hợp với bạn!", emailBody);
                                }
                            }
                        }
                        catch (Exception emailEx)
                        {
                            Console.WriteLine($"[LỖI GỬI EMAIL GỢI Ý JOB]: {emailEx.Message}");
                        }
                    }); // Hết Task.Run
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

        [HttpGet]
        public async Task<IActionResult> GetAllUsers()
        {
            try
            {
                // Lấy danh sách user và sắp xếp người mới đăng ký lên đầu
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
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi lấy dữ liệu người dùng", error = ex.Message });
            }
        }

        public class ToggleStatusDto
        {
            public bool TrangThai { get; set; }
        }

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

                // Chốt chặn bảo mật: Nếu là Admin (VaiTro == 0) thì không cho phép khóa (TrangThai = false)
                if (user.VaiTro == 0 && dto.TrangThai == false)
                {
                    return BadRequest(new { message = "Không thể khóa tài khoản của Quản trị viên!" });
                }

                // Cập nhật trạng thái
                user.TrangThai = dto.TrangThai;
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Cập nhật trạng thái thành công!" });
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống khi cập nhật trạng thái", error = ex.Message });
            }
        }
    }
}
