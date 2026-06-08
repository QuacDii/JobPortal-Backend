using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IEmailService _emailService;

        public ServiceController(JobPortalDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // 1. LẤY DANH SÁCH GÓI DỊCH VỤ
        [HttpGet("packages")]
        public async Task<IActionResult> GetPackages()
        {
            var packages = await _context.GoiDichVus.ToListAsync(); //
            return Ok(packages);
        }

        // 2. NGHIỆP VỤ MUA GÓI (Sử dụng Transaction ACID)
        [HttpPost("purchase")]
        public async Task<IActionResult> PurchasePackage([FromBody] PurchaseRequestDto request)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (userIdClaim == null) return Unauthorized(new { message = "Vui lòng đăng nhập!" });
            int maUser = int.Parse(userIdClaim.Value);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);
                var package = await _context.GoiDichVus.FirstOrDefaultAsync(g => g.MaGoi == request.MaGoi);

                if (user == null || package == null)
                    return BadRequest(new { message = "Dữ liệu không hợp lệ." });

                // KIỂM TRA GIÁ THỰC TẾ: Ưu tiên lấy giá khuyến mãi nếu có
                decimal giaThucTe = (package.GiaKhuyenMai.HasValue && package.GiaKhuyenMai > 0)
                                    ? package.GiaKhuyenMai.Value
                                    : package.GiaTien;

                if (user.SoDuVi < giaThucTe)
                    return BadRequest(new { message = "Số dư ví không đủ. Vui lòng nạp thêm tiền!" });

                // 1. TRỪ TIỀN VÍ
                user.SoDuVi -= giaThucTe;

                // 2. LOGIC ROLLOVER: CỘNG DỒN LƯỢT XEM CV
                if (user.LuotXemCvConLai.ToString()==null)
                    user.LuotXemCvConLai = 0;
                user.LuotXemCvConLai += package.SoLuotXemCv;

                // LOGIC LỊCH VẠN NIÊN THEO LOẠI GÓI
                DateTime ngayBatDau = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi > DateTime.Now
                                      ? user.NgayHetHanGoi.Value
                                      : DateTime.Now;

                switch (package.LoaiGoi)
                {
                    case 1: // Gói Ngày/Tuần
                        user.NgayHetHanGoi = ngayBatDau.AddDays(package.DonViThoiGian ?? 0);
                        break;
                    case 2: // Gói Tháng (Tự động canh tháng 28/30/31 ngày)
                        user.NgayHetHanGoi = ngayBatDau.AddMonths(package.DonViThoiGian ?? 0);
                        break;
                    case 3: // Gói Năm (Tự động canh năm nhuận)
                        user.NgayHetHanGoi = ngayBatDau.AddYears(package.DonViThoiGian ?? 0);
                        break;
                    default:
                        user.NgayHetHanGoi = ngayBatDau.AddDays(package.DonViThoiGian ?? 0);
                        break;
                }

                // 4. GHI LOG GIAO DỊCH
                var giaoDich = new GiaoDich
                {
                    MaUser = maUser,
                    MaGoi = package.MaGoi,
                    LoaiGiaoDich = 2,
                    SoTien = giaThucTe, // Lưu giá thực tế đã trừ
                    PhuongThuc = "Ví nội bộ",
                    NgayGd = DateTime.Now,
                    TrangThai = true
                };
                _context.GiaoDiches.Add(giaoDich);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // GỬI EMAIL BIÊN LAI (Bọc Try-Catch để không làm hỏng app nếu mail lỗi)
                try
                {
                    string emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; padding: 20px; border-radius: 8px;'>
                    <div style='text-align: center; border-bottom: 2px solid #D82D8B; padding-bottom: 15px; margin-bottom: 20px;'>
                        <h2 style='color: #D82D8B; margin: 0;'>BIÊN LAI ĐIỆN TỬ</h2>
                        <p style='color: #666; margin: 5px 0 0 0;'>JobsNow - Hệ thống Tuyển dụng Chuyên nghiệp</p>
                    </div>
                    <p>Xin chào <b>{user.HoTen}</b>,</p>
                    <p>Giao dịch mua gói dịch vụ của bạn đã được thực hiện <b>thành công</b>. Dưới đây là thông tin chi tiết:</p>
                    
                    <table style='width: 100%; border-collapse: collapse; margin: 20px 0; background-color: #f9f9f9;'>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Mã giao dịch:</b></td><td style='padding: 10px; border: 1px solid #eee;'>#{giaoDich.MaGd}</td></tr>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Thời gian:</b></td><td style='padding: 10px; border: 1px solid #eee;'>{giaoDich.NgayGd.ToString("dd/MM/yyyy HH:mm")}</td></tr>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Gói dịch vụ:</b></td><td style='padding: 10px; border: 1px solid #eee; color: #0056b3; font-weight: bold;'>{package.TenGoi}</td></tr>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Số tiền thanh toán:</b></td><td style='padding: 10px; border: 1px solid #eee;'>{giaThucTe.ToString("N0")} VNĐ</td></tr>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Trạng thái:</b></td><td style='padding: 10px; border: 1px solid #eee; color: #389e0d; font-weight: bold;'>Thành công</td></tr>
                    </table>
                    
                    <div style='background-color: #fffbe6; border-left: 4px solid #faad14; padding: 12px; margin-top: 20px;'>
                        <p style='margin: 0; font-size: 16px;'>⏳ <b>Hạn sử dụng gói dịch vụ hiện tại:</b> <span style='color: #cf1322; font-weight: bold;'>{user.NgayHetHanGoi?.ToString("dd/MM/yyyy")}</span></p>
                    </div>
                    
                    <p style='color: #8c8c8c; font-size: 13px; text-align: center; margin-top: 30px; border-top: 1px solid #e0e0e0; padding-top: 15px;'>Đây là email gửi tự động, vui lòng không trả lời thư này.</p>
                </div>";

                    // Bắn mail tới địa chỉ Email lưu trong bảng User
                    await _emailService.SendEmailAsync(user.Email, $"[JobsNow] Kích hoạt {package.TenGoi} thành công", emailBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LỖI GỬI EMAIL]: {ex.Message}");
                }

                return Ok(new
                {
                    message = "Đăng ký gói thành công! Quyền lợi đã được cập nhật.",
                    soDuMoi = user.SoDuVi,
                    ngayHetHanMoi = user.NgayHetHanGoi,
                    luotXemMoi = user.LuotXemCvConLai
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống khi xử lý giao dịch." });
            }
        }

        // 3. XEM LỊCH SỬ GIAO DỊCH CỦA DOANH NGHIỆP
        [HttpGet("history")]
        public async Task<IActionResult> GetTransactionHistory()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (userIdClaim == null) return Unauthorized();
            int maUser = int.Parse(userIdClaim.Value);

            var history = await _context.GiaoDiches
                .Where(g => g.MaUser == maUser)
                .OrderByDescending(g => g.NgayGd)
                .Select(g => new {
                    g.MaGd,
                    g.LoaiGiaoDich,
                    g.SoTien,
                    g.PhuongThuc,
                    g.NgayGd,
                    g.TrangThai,
                    TenGoi = g.MaGoiNavigation != null ? g.MaGoiNavigation.TenGoi : "Nạp tiền" // Lấy tên gói nếu có
                })
                .ToListAsync();

            return Ok(history);
        }

        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (userIdClaim == null) return Unauthorized(new { message = "Vui lòng đăng nhập" });

            int maUser = int.Parse(userIdClaim.Value);
            var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);

            if (user == null) return NotFound();

            // TÌM GIAO DỊCH MUA GÓI GẦN NHẤT
            var latestTx = await _context.GiaoDiches
                .Include(g => g.MaGoiNavigation) // Join sang bảng GoiDichVu
                .Where(g => g.MaUser == maUser && g.LoaiGiaoDich == 2 && g.TrangThai == true)
                .OrderByDescending(g => g.NgayGd)
                .FirstOrDefaultAsync();

            string tenGoi = "Miễn phí";
            DateTime? ngayMua = null;

            // Nếu người dùng vẫn đang còn hạn sử dụng gói
            if (user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi > DateTime.Now)
            {
                tenGoi = latestTx != null && latestTx.MaGoiNavigation != null
                         ? latestTx.MaGoiNavigation.TenGoi
                         : "Tài khoản VIP";
                ngayMua = latestTx?.NgayGd;
            }

            return Ok(new
            {
                soDuVi = user.SoDuVi,
                ngayHetHanGoi = user.NgayHetHanGoi,
                luotXemCvConLai = user.LuotXemCvConLai,
                tenGoiHienTai = tenGoi,
                ngayMua = ngayMua
            });
        }
    }

    public class PurchaseRequestDto
    {
        public int MaGoi { get; set; }
    }
}