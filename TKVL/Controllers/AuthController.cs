using Google.Apis.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TKVL.Dtos;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;

        public AuthController(JobPortalDbContext context, IConfiguration config, IEmailService emailService)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            try
            {
                using var httpClient = new HttpClient();
                var googleResponse = await httpClient.GetAsync($"https://www.googleapis.com/oauth2/v3/userinfo?access_token={dto.AccessToken}");

                if (!googleResponse.IsSuccessStatusCode)
                {
                    return BadRequest(new { success = false, message = "Mã xác thực Google không hợp lệ hoặc đã hết hạn!" });
                }

                var payload = await googleResponse.Content.ReadFromJsonAsync<GoogleUserInfoDto>();
                if (payload == null)
                {
                    return BadRequest(new { success = false, message = "Không thể lấy thông tin tài khoản từ Google!" });
                }

                string email = payload.Email;
                string name = payload.Name;
                string googleId = payload.Sub;
                string avatar = payload.Picture;

                var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId || u.Email == email);

                if (user == null)
                {
                    user = new User
                    {
                        Email = email,
                        MatKhau = null,
                        HoTen = name,
                        Avatar = avatar,
                        GoogleId = googleId,
                        VaiTro = dto.VaiTro ?? 2,
                        SoDuVi = 0,
                        TrangThai = true,
                        NgayTao = DateTime.Now,
                        LuotXemCvConLai = dto.VaiTro == 1 ? 10 : 0
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else if (string.IsNullOrEmpty(user.GoogleId))
                {
                    user.GoogleId = googleId;
                    if (string.IsNullOrEmpty(user.Avatar)) user.Avatar = avatar;
                    await _context.SaveChangesAsync();
                }

                if (!user.TrangThai) return BadRequest(new { success = false, message = "Tài khoản của bạn đã bị khóa!" });

                // KIỂM TRA ĐẶC QUYỀN VIP
                bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
                    new Claim("HoTen", user.HoTen),
                    // GHI TRẠNG THÁI VIP VÀO TOKEN
                    new Claim("isVip", isVip.ToString().ToLower())
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:DurationInMinutes"])),
                    Issuer = _config["Jwt:Issuer"],
                    Audience = _config["Jwt:Audience"],
                    SigningCredentials = creds
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var securityToken = tokenHandler.CreateToken(tokenDescriptor);
                string systemToken = tokenHandler.WriteToken(securityToken);

                string newRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                user.RefreshToken = newRefreshToken;
                user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, token = systemToken, message = "Đăng nhập bằng tài khoản Google thành công!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi hệ thống khi xác thực Google!", error = ex.Message });
            }
        }

        [HttpPost("facebook-login")]
        public async Task<IActionResult> FacebookLogin([FromBody] GoogleLoginDto dto)
        {
            try
            {
                using var httpClient = new HttpClient();
                var fbResponse = await httpClient.GetAsync($"https://graph.facebook.com/me?fields=id,name,email,picture&access_token={dto.AccessToken}");

                if (!fbResponse.IsSuccessStatusCode)
                {
                    return BadRequest(new { success = false, message = "Mã xác thực Facebook không hợp lệ hoặc đã hết hạn!" });
                }

                var payload = await fbResponse.Content.ReadFromJsonAsync<FacebookUserInfoDto>();
                if (payload == null)
                {
                    return BadRequest(new { success = false, message = "Không thể lấy thông tin tài khoản từ Facebook!" });
                }

                string email = payload.Email;
                if (string.IsNullOrEmpty(email))
                {
                    email = $"{payload.Id}@facebook.com";
                }
                string name = payload.Name;
                string facebookId = payload.Id;
                string avatar = payload.Picture?.Data?.Url;

                var user = await _context.Users.FirstOrDefaultAsync(u => u.FacebookId == facebookId || u.Email == email);

                if (user == null)
                {
                    user = new User
                    {
                        Email = email,
                        MatKhau = null,
                        HoTen = name,
                        Avatar = avatar,
                        FacebookId = facebookId,
                        VaiTro = dto.VaiTro ?? 2,
                        SoDuVi = 0,
                        TrangThai = true,
                        NgayTao = DateTime.Now,
                        LuotXemCvConLai = dto.VaiTro == 1 ? 10 : 0
                    };
                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else if (string.IsNullOrEmpty(user.FacebookId))
                {
                    user.FacebookId = facebookId;
                    if (string.IsNullOrEmpty(user.Avatar)) user.Avatar = avatar;
                    await _context.SaveChangesAsync();
                }

                if (!user.TrangThai) return BadRequest(new { success = false, message = "Tài khoản của bạn đã bị khóa!" });

                // KIỂM TRA ĐẶC QUYỀN VIP
                bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
                    new Claim("HoTen", user.HoTen),
                    new Claim("isVip", isVip.ToString().ToLower())
                };

                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                var tokenDescriptor = new SecurityTokenDescriptor
                {
                    Subject = new ClaimsIdentity(claims),
                    Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:DurationInMinutes"])),
                    Issuer = _config["Jwt:Issuer"],
                    Audience = _config["Jwt:Audience"],
                    SigningCredentials = creds
                };

                var tokenHandler = new JwtSecurityTokenHandler();
                var securityToken = tokenHandler.CreateToken(tokenDescriptor);
                string systemToken = tokenHandler.WriteToken(securityToken);

                string newRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                user.RefreshToken = newRefreshToken;
                user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, token = systemToken, message = "Đăng nhập bằng Facebook thành công!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi hệ thống khi xác thực Facebook!", error = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                return Ok(new { success = true, message = "Nếu Email tồn tại trên hệ thống, một liên kết khôi phục đã được gửi đi!" });
            }

            string resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

            user.ResetToken = resetToken;
            user.ResetTokenExpiry = DateTime.Now.AddMinutes(15);
            await _context.SaveChangesAsync();

            string resetLink = $"http://localhost:5173/reset-password?token={resetToken}";

            try
            {
                string emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #ddd; padding: 20px; border-radius: 10px;'>
                    <h2 style='color: #1890ff; text-align: center;'>Yêu Cầu Khôi Phục Mật Khẩu</h2>
                    <p>Chào bạn,</p>
                    <p>Hệ thống JobsNow đã nhận được yêu cầu khôi phục mật khẩu từ tài khoản của bạn.</p>
                    <p>Vui lòng click vào nút bên dưới để thiết lập mật khẩu mới (Liên kết này chỉ có hiệu lực trong 15 phút):</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetLink}' style='background-color: #1890ff; color: white; padding: 12px 24px; text-decoration: none; border-radius: 5px; font-weight: bold;'>Đổi Mật Khẩu Ngay</a>
                    </div>
                    <p style='color: #8c8c8c; font-size: 13px;'>Nếu bạn không yêu cầu đổi mật khẩu, vui lòng bỏ qua email này. Khóa bảo mật của bạn vẫn an toàn.</p>
                    <hr style='border-top: 1px solid #eee;'/>
                    <p style='text-align: center; color: #8c8c8c; font-size: 12px;'>Đội ngũ JobsNow Hỗ trợ</p>
                </div>
                ";

                await _emailService.SendEmailAsync(user.Email, "[JobsNow] Khôi phục mật khẩu của bạn", emailBody);

                return Ok(new { success = true, message = "Liên kết đặt lại mật khẩu đã được gửi tới Email của bạn!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Không thể gửi email lúc này. Vui lòng thử lại sau!", error = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetToken == dto.Token && u.ResetTokenExpiry > DateTime.Now);

            if (user == null)
            {
                return BadRequest(new { success = false, message = "Liên kết khôi phục mật khẩu không hợp lệ hoặc đã hết hạn!" });
            }

            user.MatKhau = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);

            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập ngay bây giờ." });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] DangKyDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { success = false, message = "Email này đã được đăng ký sử dụng!" });
            }

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau);

            var newUser = new User
            {
                Email = dto.Email,
                MatKhau = hashedPassword,
                HoTen = dto.HoTen,
                VaiTro = dto.VaiTro,
                SoDuVi = 0,
                TrangThai = true,
                NgayTao = DateTime.Now,
                LuotXemCvConLai = 0
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đăng ký tài khoản thành công!" });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] DangNhapDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Tài khoản email hoặc mật khẩu không chính xác!" });
            }

            if (!user.TrangThai)
            {
                return BadRequest(new { success = false, message = "Tài khoản của bạn hiện đã bị khóa bởi Admin!" });
            }

            if (string.IsNullOrEmpty(user.MatKhau))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Tài khoản này được kết nối qua Google/Facebook. Vui lòng đăng nhập bằng Mạng xã hội hoặc dùng 'Quên mật khẩu' để tạo mật khẩu mới!"
                });
            }

            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.MatKhau, user.MatKhau);
            if (!isPasswordValid)
            {
                return Unauthorized(new { success = false, message = "Tài khoản email hoặc mật khẩu không chính xác!" });
            }

            // 👉 KIỂM TRA ĐẶC QUYỀN VIP
            bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
                new Claim("HoTen", user.HoTen),
                // 👉 GHI TRẠNG THÁI VIP VÀO TOKEN
                new Claim("isVip", isVip.ToString().ToLower())
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:DurationInMinutes"])),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                SigningCredentials = creds
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(token);
            string jwtToken = tokenHandler.WriteToken(securityToken);

            string newRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            user.RefreshToken = newRefreshToken;
            user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, token = jwtToken, message = "Đăng nhập hệ thống thành công!" });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> Refresh([FromBody] TokenRequestDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);

            if (user == null || user.NgayHetHanRefreshToken < DateTime.Now)
            {
                return Unauthorized(new { success = false, message = "Phiên làm việc đã hết hạn, vui lòng đăng nhập lại!" });
            }

            // 👉 KIỂM TRA ĐẶC QUYỀN VIP KHI LÀM MỚI TOKEN (Có thể VIP vừa hết hạn)
            bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;

            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
                new Claim("HoTen", user.HoTen),
                // 👉 GHI LẠI TRẠNG THÁI VIP MỚI NHẤT
                new Claim("isVip", isVip.ToString().ToLower())
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(_config["Jwt:DurationInMinutes"])),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                SigningCredentials = creds
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            string newAccessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

            string newRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            user.RefreshToken = newRefreshToken;
            user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, accessToken = newAccessToken, refreshToken = newRefreshToken });
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "1")]
        [HttpGet("employer-status")]
        public async Task<IActionResult> GetEmployerStatus()
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được danh tính." });
                }

                var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == userId);

                if (company == null)
                {
                    return Ok(new { success = true, status = "NO_COMPANY" });
                }

                if (company.TrangThai == false)
                {
                    return Ok(new { success = true, status = "PENDING" });
                }

                return Ok(new { success = true, status = "APPROVED" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [Microsoft.AspNetCore.Authorization.Authorize(Roles = "1")]
        [HttpPost("onboarding")]
        public async Task<IActionResult> SubmitOnboarding([FromBody] OnboardingDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    return Unauthorized(new { success = false, message = "Không xác định được danh tính." });
                }

                var existingCompany = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == userId);
                if (existingCompany != null)
                {
                    return BadRequest(new { success = false, message = "Hồ sơ công ty của bạn đã tồn tại trên hệ thống!" });
                }

                var newCompany = new CongTy
                {
                    MaUser = userId,
                    TenCongTy = dto.TenCongTy,
                    MaSoThue = dto.MaSoThue,
                    DiaChi = dto.DiaChi,
                    QuyMo = dto.QuyMo ?? "Dưới 50 nhân viên",
                    MoTa = dto.MoTa ?? "",
                    TrangThai = false
                };

                _context.CongTies.Add(newCompany);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Gửi hồ sơ doanh nghiệp thành công! Vui lòng đợi Admin kiểm duyệt." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        public class TokenRequestDto
        {
            public string RefreshToken { get; set; } = null!;
        }
        public class FacebookUserInfoDto
        {
            public string Id { get; set; } = null!;
            public string Name { get; set; } = null!;
            public string Email { get; set; } = null!;
            public FacebookPictureDto Picture { get; set; }
        }

        public class FacebookPictureDto
        {
            public FacebookPictureDataDto Data { get; set; }
        }

        public class FacebookPictureDataDto
        {
            public string Url { get; set; } = null!;
        }

        public class ForgotPasswordDto
        {
            public string Email { get; set; } = null!;
        }

        public class ResetPasswordDto
        {
            public string Token { get; set; } = null!;
            public string NewPassword { get; set; } = null!;
        }
        public class OnboardingDto
        {
            public string TenCongTy { get; set; } = null!;
            public string MaSoThue { get; set; } = null!;
            public string DiaChi { get; set; } = null!;
            public string? QuyMo { get; set; }
            public string? MoTa { get; set; }
        }
    }
}