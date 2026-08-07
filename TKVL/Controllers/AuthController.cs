using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto;
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

        // ==============================================================
        // 1. API CẬP NHẬT EMAIL MỚI (DÀNH CHO TÀI KHOẢN FACEBOOK / EMAIL ÁO)
        // ==============================================================
        [HttpPut("update-email")]
        public async Task<IActionResult> UpdateEmail([FromBody] UpdateEmailDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.NewEmail))
                return BadRequest(new { success = false, message = "Email mới không được để trống!" });

            var user = await _context.Users.FindAsync(dto.UserId);
            if (user == null)
                return NotFound(new { success = false, message = "Người dùng không tồn tại!" });

            // Kiểm tra xem email mới có bị trùng với tài khoản khác không
            bool isEmailTaken = await _context.Users.AnyAsync(u => u.Email == dto.NewEmail && u.MaUser != dto.UserId);
            if (isEmailTaken)
                return BadRequest(new { success = false, message = "Email này đã được sử dụng bởi tài khoản khác!" });

            // Cập nhật Email mới và đặt trạng thái chưa xác thực
            user.Email = dto.NewEmail;
            user.IsEmailVerified = false;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Cập nhật Email thành công! Vui lòng nhận mã OTP để xác thực." });
        }

        // ==============================================================
        // 2. API GỬI MÃ OTP VỀ EMAIL
        // ==============================================================
        [HttpPost("send-otp")]
        public async Task<IActionResult> SendOtp([FromBody] SendOtpDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { success = false, message = "Email không được để trống!" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
                return NotFound(new { success = false, message = "Không tìm thấy tài khoản với email này!" });

            // Sinh mã ngẫu nhiên 6 chữ số
            string otp = Random.Shared.Next(100000, 999999).ToString();

            user.OtpCode = otp;
            user.OtpExpiry = DateTime.Now.AddMinutes(5);
            await _context.SaveChangesAsync();

            string htmlBody = $@"
            <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #f4f5f5;'>
                <div style='max-width: 500px; margin: 0 auto; background: #ffffff; padding: 30px; border-radius: 12px; border: 1px solid #e8e8e8;'>
                    <h2 style='color: #1890ff; text-align: center;'>Mã Xác Thực JobsNow</h2>
                    <p>Xin chào <b>{user.HoTen ?? "Ứng viên"}</b>,</p>
                    <p>Mã OTP xác nhận Email của bạn là:</p>
                    <div style='text-align: center; margin: 24px 0;'>
                        <span style='font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #1890ff; background: #e6f7ff; padding: 10px 24px; border-radius: 8px;'>{otp}</span>
                    </div>
                    <p style='color: #8c8c8c; font-size: 13px;'>Mã này có hiệu lực trong vòng <b>5 phút</b>. Vui lòng không chia sẻ mã này cho bất kỳ ai.</p>
                </div>
            </div>";

            try
            {
                await _emailService.SendEmailAsync(dto.Email, "[JobsNow] Mã xác thực tài khoản", htmlBody);
                return Ok(new { success = true, message = "Đã gửi mã OTP thành công về Email!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi gửi email: " + ex.Message });
            }
        }

        // ==============================================================
        // 3. API XÁC THỰC MÃ OTP
        // ==============================================================
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null) return NotFound(new { success = false, message = "Tài khoản không tồn tại!" });

            if (user.OtpCode != dto.OtpCode) return BadRequest(new { success = false, message = "Mã OTP không chính xác!" });
            if (user.OtpExpiry == null || user.OtpExpiry < DateTime.Now) return BadRequest(new { success = false, message = "Mã OTP đã hết hạn!" });

            // 1. Cập nhật DB
            user.IsEmailVerified = true;
            user.OtpCode = null;
            user.OtpExpiry = null;
            await _context.SaveChangesAsync();

            // 2. 🌟 TẠO TOKEN MỚI CHỨA CỜ isEmailVerified = true
            bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;
            var claims = new[]
            {
        new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
        new Claim("HoTen", user.HoTen ?? ""),
        new Claim("isVip", isVip.ToString().ToLower()),
        new Claim("isEmailVerified", "true") // Đã xác thực
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
            string newToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

            // 3. Trả về token mới cho Frontend
            return Ok(new { success = true, token = newToken, message = "Xác thực Email thành công!" });
        }

        // ==============================================================
        // 4. API ĐĂNG NHẬP GOOGLE
        // ==============================================================
        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            try
            {
                using var httpClient = new HttpClient();
                var googleResponse = await httpClient.GetAsync($"https://www.googleapis.com/oauth2/v3/userinfo?access_token={dto.AccessToken}");

                if (!googleResponse.IsSuccessStatusCode)
                    return BadRequest(new { success = false, message = "Mã xác thực Google không hợp lệ hoặc đã hết hạn!" });

                var payload = await googleResponse.Content.ReadFromJsonAsync<GoogleUserInfoDto>();
                if (payload == null)
                    return BadRequest(new { success = false, message = "Không thể lấy thông tin tài khoản từ Google!" });

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
                        IsEmailVerified = true, 
                        NgayTao = DateTime.Now,
                        LuotXemCvConLai = dto.VaiTro == 1 ? 10 : 0
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else if (string.IsNullOrEmpty(user.GoogleId))
                {
                    user.GoogleId = googleId;
                    user.IsEmailVerified = true;
                    if (string.IsNullOrEmpty(user.Avatar)) user.Avatar = avatar;
                    await _context.SaveChangesAsync();
                }

                if (!user.TrangThai) return BadRequest(new { success = false, message = "Tài khoản của bạn đã bị khóa!" });

                bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;
                bool isVerified = user.VaiTro == 0 || user.IsEmailVerified;

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
                    new Claim("HoTen", user.HoTen),
                    new Claim("isVip", isVip.ToString().ToLower()),
                    new Claim("isEmailVerified", isVerified.ToString().ToLower()),
                    new Claim("NgayHetHanGoi", user.NgayHetHanGoi?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "")
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
                string systemToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

                user.RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, token = systemToken, message = "Đăng nhập Google thành công!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi hệ thống khi xác thực Google!", error = ex.Message });
            }
        }

        // ==============================================================
        // 5. API ĐĂNG NHẬP FACEBOOK
        // ==============================================================
        [HttpPost("facebook-login")]
        public async Task<IActionResult> FacebookLogin([FromBody] GoogleLoginDto dto)
        {
            try
            {
                using var httpClient = new HttpClient();
                var fbResponse = await httpClient.GetAsync($"https://graph.facebook.com/me?fields=id,name,email,picture&access_token={dto.AccessToken}");

                if (!fbResponse.IsSuccessStatusCode)
                    return BadRequest(new { success = false, message = "Mã xác thực Facebook không hợp lệ hoặc đã hết hạn!" });

                var payload = await fbResponse.Content.ReadFromJsonAsync<FacebookUserInfoDto>();
                if (payload == null)
                    return BadRequest(new { success = false, message = "Không thể lấy thông tin tài khoản từ Facebook!" });

                bool isFakeEmail = string.IsNullOrEmpty(payload.Email);
                string email = isFakeEmail ? $"{payload.Id}@facebook.com" : payload.Email;
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
                        IsEmailVerified = !isFakeEmail, 
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

                bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;
                bool isVerified = user.VaiTro == 0 || user.IsEmailVerified;

                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
                    new Claim("HoTen", user.HoTen),
                    new Claim("isVip", isVip.ToString().ToLower()),
                    new Claim("isEmailVerified", isVerified.ToString().ToLower()),
                    new Claim("NgayHetHanGoi", user.NgayHetHanGoi?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "")
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
                string systemToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

                user.RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
                user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7);
                await _context.SaveChangesAsync();

                // 🌟 Trả về thông tin requireUpdateEmail để React nhận diện mở Popup
                return Ok(new
                {
                    success = true,
                    token = systemToken,
                    requireUpdateEmail = user.Email.EndsWith("@facebook.com") || !user.IsEmailVerified,
                    message = "Đăng nhập Facebook thành công!"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi hệ thống khi xác thực Facebook!", error = ex.Message });
            }
        }

        // ==============================================================
        // 6. CÁC API KHÁC (LOGIN, REGISTER, FORGOT PASSWORD...)
        // ==============================================================
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
                    <p style='color: #8c8c8c; font-size: 13px;'>Nếu bạn không yêu cầu đổi mật khẩu, vui lòng bỏ qua email này.</p>
                </div>";

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
                return BadRequest(new { success = false, message = "Liên kết khôi phục mật khẩu không hợp lệ hoặc đã hết hạn!" });

            user.MatKhau = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập ngay." });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] DangKyDto dto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest(new { success = false, message = "Email này đã được đăng ký sử dụng!" });

            var newUser = new User
            {
                Email = dto.Email,
                MatKhau = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau),
                HoTen = dto.HoTen,
                VaiTro = dto.VaiTro,
                SoDuVi = 0,
                TrangThai = true,
                IsEmailVerified = dto.VaiTro == 0,
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
            if (user == null || string.IsNullOrEmpty(user.MatKhau) || !BCrypt.Net.BCrypt.Verify(dto.MatKhau, user.MatKhau))
                return Unauthorized(new { success = false, message = "Tài khoản email hoặc mật khẩu không chính xác!" });

            if (!user.TrangThai)
                return BadRequest(new { success = false, message = "Tài khoản của bạn hiện đã bị khóa bởi Admin!" });

            bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;
            bool isVerified = user.VaiTro == 0 || user.IsEmailVerified;

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
                new Claim("HoTen", user.HoTen),
                new Claim("isVip", isVip.ToString().ToLower()),
                new Claim("isEmailVerified", isVerified.ToString().ToLower()),
                new Claim("NgayHetHanGoi", user.NgayHetHanGoi?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "")
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
            string jwtToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));

            user.RefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, token = jwtToken, message = "Đăng nhập hệ thống thành công!" });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> Refresh([FromBody] TokenRequestDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);
            if (user == null || user.NgayHetHanRefreshToken < DateTime.Now)
                return Unauthorized(new { success = false, message = "Phiên làm việc đã hết hạn, vui lòng đăng nhập lại!" });

            bool isVip = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value > DateTime.UtcNow;
            bool isVerified = user.VaiTro == 0 || user.IsEmailVerified;

            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
                new Claim("HoTen", user.HoTen),
                new Claim("isVip", isVip.ToString().ToLower()),
                new Claim("isEmailVerified", isVerified.ToString().ToLower()),
                new Claim("NgayHetHanGoi", user.NgayHetHanGoi?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "")
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

            // 2. Gán biến vào User
            user.RefreshToken = newRefreshToken;
            user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, accessToken = newAccessToken, refreshToken = newRefreshToken });
        }

        [Authorize(Roles = "1")]
        [HttpGet("employer-status")]
        public async Task<IActionResult> GetEmployerStatus()
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { success = false, message = "Không xác định được danh tính." });

                var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == userId);
                if (company == null) return Ok(new { success = true, status = "NO_COMPANY" });
                if (company.TrangThai == false) return Ok(new { success = true, status = "PENDING" });

                return Ok(new { success = true, status = "APPROVED" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [Authorize(Roles = "1")]
        [HttpPost("onboarding")]
        public async Task<IActionResult> SubmitOnboarding([FromBody] OnboardingDto dto)
        {
            try
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                    return Unauthorized(new { success = false, message = "Không xác định được danh tính." });

                var existingCompany = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == userId);
                if (existingCompany != null)
                    return BadRequest(new { success = false, message = "Hồ sơ công ty của bạn đã tồn tại trên hệ thống!" });

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

        // --- DTOS BỔ SUNG ---
        public class UpdateEmailDto
        {
            public int UserId { get; set; }
            public string NewEmail { get; set; } = string.Empty;
        }

        public class SendOtpDto
        {
            public string Email { get; set; } = string.Empty;
        }

        public class VerifyOtpDto
        {
            public string Email { get; set; } = string.Empty;
            public string OtpCode { get; set; } = string.Empty;
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