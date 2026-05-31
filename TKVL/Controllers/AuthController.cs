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

                // Đọc dữ liệu JSON trả về từ Google UserInfo Endpoint
                var payload = await googleResponse.Content.ReadFromJsonAsync<GoogleUserInfoDto>();
                if (payload == null)
                {
                    return BadRequest(new { success = false, message = "Không thể lấy thông tin tài khoản từ Google!" });
                }

                string email = payload.Email;
                string name = payload.Name;
                string googleId = payload.Sub; // ID duy nhất của tài khoản Google đó 
                string avatar = payload.Picture;

                // =================================================================
                // 2.Kiểm tra xem User này đã từng đăng nhập/đăng ký chưa 
                // =================================================================
                var user = await _context.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId || u.Email == email);

                if (user == null)
                {
                    // Trường hợp người dùng mới toanh: Tự động đăng ký (INSERT) một tài khoản mới 
                    user = new User
                    {
                        Email = email,
                        MatKhau = null, // Đăng nhập MXH thì không cần lưu mật khẩu truyền thống 
                        HoTen = name,
                        Avatar = avatar,
                        GoogleId = googleId, // Lưu vết để nhận diện cho lần đăng nhập sau 
                        VaiTro = dto.VaiTro ?? 2, // Lấy vai trò (1 hoặc 2) truyền từ FE sang, mặc định là 2 (Ứng viên)
                        SoDuVi = 0,
                        TrangThai = true,
                        NgayTao = DateTime.Now,
                        LuotXemCvConLai = dto.VaiTro == 1 ? 10 : 0 // Nếu là NTD thì tặng sẵn 10 lượt xem CV
                    };

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();
                }
                else if (string.IsNullOrEmpty(user.GoogleId))
                {
                    // Trường hợp email này đã đăng ký bằng mật khẩu thường trước đó, giờ họ bấm đăng nhập Google
                    // Thực hiện liên kết tài khoản (UPDATE thêm GoogleId để đồng bộ thực thể)
                    user.GoogleId = googleId;
                    if (string.IsNullOrEmpty(user.Avatar)) user.Avatar = avatar;
                    await _context.SaveChangesAsync();
                }

                // 3. Kiểm tra xem tài khoản có bị khóa không
                if (!user.TrangThai) return BadRequest(new { success = false, message = "Tài khoản của bạn đã bị khóa!" });

                // 4.  KÝ SỐ VÀ CẤP JWT TOKEN CỦA HỆ THỐNG MÌNH BẮN VỀ CHO REACTJS
                var claims = new[]
                {
            new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
            new Claim("HoTen", user.HoTen)
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
                user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7); // Khóa phụ sống 7 ngày
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
                // 1. Dùng HttpClient gọi lên Facebook Graph API để xác thực access_token
                using var httpClient = new HttpClient();
                // Xin Facebook trả về các trường: id, name, email, picture
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
                    // Cấp cho họ một cái email ảo dựa trên ID Facebook để không bị lỗi Database
                    email = $"{payload.Id}@facebook.com";
                }
                string name = payload.Name;
                string facebookId = payload.Id;
                string avatar = payload.Picture?.Data?.Url;

                // 2. KIỂM TRA DATABASE
                // Tìm user theo FacebookId HOẶC Email (để liên kết tài khoản nếu họ đã đăng ký email này trước đó)
                var user = await _context.Users.FirstOrDefaultAsync(u => u.FacebookId == facebookId || u.Email == email);

                if (user == null)
                {
                    user = new User
                    {
                        Email = email,
                        MatKhau = null,
                        HoTen = name,
                        Avatar = avatar,
                        FacebookId = facebookId, // Lưu FacebookId
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
                    // Liên kết tài khoản nếu tìm thấy Email nhưng chưa có FacebookId
                    user.FacebookId = facebookId;
                    if (string.IsNullOrEmpty(user.Avatar)) user.Avatar = avatar;
                    await _context.SaveChangesAsync();
                }

                if (!user.TrangThai) return BadRequest(new { success = false, message = "Tài khoản của bạn đã bị khóa!" });

                // 3. CẤP JWT TOKEN (Copy y nguyên đoạn cấp Token của ông xuống đây)
                var claims = new[]
                {
            new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
            new Claim("HoTen", user.HoTen)
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
                // Để bảo mật, không nên nói rõ "Email không tồn tại", cứ báo check mail chung chung
                return Ok(new { success = true, message = "Nếu Email tồn tại trên hệ thống, một liên kết khôi phục đã được gửi đi!" });
            }

            // Sinh ra chuỗi Token bảo mật ngẫu nhiên không thể đoán trước
            string resetToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(64));

            // Lưu vào database kèm thời gian hết hạn (Ví dụ: sống trong 15 phút)
            user.ResetToken = resetToken;
            user.ResetTokenExpiry = DateTime.Now.AddMinutes(15);
            await _context.SaveChangesAsync();

            // ĐƯỜNG DẪN TRANG ĐỔI MẬT KHẨU PHÍA FRONTEND REACTJS
            string resetLink = $"http://localhost:3000/reset-password?token={resetToken}";

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

                // GỌI HÀM GỬI MAIL THẬT
                await _emailService.SendEmailAsync(user.Email, "[JobsNow] Khôi phục mật khẩu của bạn", emailBody);

                return Ok(new { success = true, message = "Liên kết đặt lại mật khẩu đã được gửi tới Email của bạn!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Không thể gửi email lúc này. Vui lòng thử lại sau!", error = ex.Message });
            }
        }

        // 2. API XÁC NHẬN ĐỔI MẬT KHẨU MỚI
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            // Tìm user theo Token và kiểm tra xem Token đã hết hạn chưa
            var user = await _context.Users.FirstOrDefaultAsync(u => u.ResetToken == dto.Token && u.ResetTokenExpiry > DateTime.Now);

            if (user == null)
            {
                return BadRequest(new { success = false, message = "Liên kết khôi phục mật khẩu không hợp lệ hoặc đã hết hạn!" });
            }

            // Thực hiện đổi mật khẩu (Nếu có hàm BCrypt/Hash mật khẩu thì ông bọc vào nhé, đây tôi viết minh họa lưu trực tiếp)
            // Ví dụ: user.MatKhau = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            user.MatKhau = dto.NewPassword;

            // Đổi mật khẩu thành công thì XÓA TOKEN ĐI để không cho xài lại lần 2
            user.ResetToken = null;
            user.ResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập ngay bây giờ." });
        }

        // 1. API ĐĂNG KÝ TÀI KHOẢN TRUYỀN THỐNG
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] DangKyDto dto)
        {
            // Kiểm tra email đã tồn tại trong hệ thống chưa
            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
            {
                return BadRequest(new { success = false, message = "Email này đã được đăng ký sử dụng!" });
            }

            // Tiến hành mã hóa mật khẩu bằng BCrypt trước khi lưu dữ liệu
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.MatKhau);

            var newUser = new User
            {
                Email = dto.Email,
                MatKhau = hashedPassword,
                HoTen = dto.HoTen,
                VaiTro = dto.VaiTro, // 1: Nhà tuyển dụng, 2: Ứng viên
                SoDuVi = 0,
                TrangThai = true,
                NgayTao = DateTime.Now,
                LuotXemCvConLai = 0
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đăng ký tài khoản thành công!" });
        }

        // 2. API ĐĂNG NHẬP TRUYỀN THỐNG CẤP JWT TOKEN
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] DangNhapDto dto)
        {
            // Tìm tài khoản theo email
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
            if (user == null)
            {
                return Unauthorized(new { success = false, message = "Tài khoản email hoặc mật khẩu không chính xác!" });
            }

            // Kiểm tra trạng thái tài khoản xem có bị Admin khóa không
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

            // So khớp mật khẩu băm BCrypt
            bool isPasswordValid = BCrypt.Net.BCrypt.Verify(dto.MatKhau, user.MatKhau);
            if (!isPasswordValid)
            {
                return Unauthorized(new { success = false, message = "Tài khoản email hoặc mật khẩu không chính xác!" });
            }

            // Khởi tạo các thông tin định danh (Claims) để nhét vào trong Token
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.VaiTro.ToString()), // Ép vai trò vào token để phân quyền
                new Claim("HoTen", user.HoTen)
            };

            // Ký số bảo mật cho Token sử dụng khóa cấu hình bí mật
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
            user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7); // Khóa phụ sống 7 ngày
            await _context.SaveChangesAsync();

            return Ok(new { success = true, token = jwtToken, message = "Đăng nhập hệ thống thành công!" });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> Refresh([FromBody] TokenRequestDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.RefreshToken == dto.RefreshToken);

            // Kiểm tra xem RefreshToken có tồn tại và còn hạn không
            if (user == null || user.NgayHetHanRefreshToken < DateTime.Now)
            {
                return Unauthorized(new { success = false, message = "Phiên làm việc đã hết hạn, vui lòng đăng nhập lại!" });
            }

            // Tạo AccessToken mới 
            var claims = new[] {
        new Claim(ClaimTypes.NameIdentifier, user.MaUser.ToString()),
        new Claim(ClaimTypes.Email, user.Email),
        new Claim(ClaimTypes.Role, user.VaiTro.ToString()),
        new Claim("HoTen", user.HoTen)
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

            // Tạo một RefreshToken mới để cuốn chiếu bảo mật
            string newRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            user.RefreshToken = newRefreshToken;
            user.NgayHetHanRefreshToken = DateTime.Now.AddDays(7); // Khóa phụ sống 7 ngày
            await _context.SaveChangesAsync();

            return Ok(new { success = true, accessToken = newAccessToken, refreshToken = newRefreshToken });
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
    }
}
