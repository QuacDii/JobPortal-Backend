using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    public class ServiceController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IServiceProvider _serviceProvider; // 🌟 1. Bổ sung IServiceProvider

        public ServiceController(JobPortalDbContext context, IEmailService emailService, IServiceProvider serviceProvider)
        {
            _context = context;
            _emailService = emailService;
            _serviceProvider = serviceProvider;
        }

        // 1. LẤY DANH SÁCH GÓI DỊCH VỤ
        [HttpGet("packages")]
        public async Task<IActionResult> GetPackages()
        {
            var packages = await _context.GoiDichVus.ToListAsync();
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

                // KIỂM TRA GIÁ THỰC TẾ
                decimal giaThucTe = (package.GiaKhuyenMai.HasValue && package.GiaKhuyenMai > 0)
                                    ? package.GiaKhuyenMai.Value
                                    : package.GiaTien;

                if (user.SoDuVi < giaThucTe)
                    return BadRequest(new { message = "Số dư ví không đủ. Vui lòng nạp thêm tiền!" });

                // 1. TRỪ TIỀN VÍ
                user.SoDuVi -= giaThucTe;

                // 2. LOGIC ROLLOVER: CỘNG DỒN LƯỢT XEM CV
                if (user.LuotXemCvConLai == null)
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
                    case 2: // Gói Tháng
                        user.NgayHetHanGoi = ngayBatDau.AddMonths(package.DonViThoiGian ?? 0);
                        break;
                    case 3: // Gói Năm
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
                    SoTien = giaThucTe,
                    PhuongThuc = "Ví nội bộ",
                    NgayGd = DateTime.Now,
                    TrangThai = true
                };

                _context.GiaoDiches.Add(giaoDich);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 🌟 2. KÍCH HOẠT PHÂN TÍCH AI BÙ BẮT ĐẦU TỪ ĐÂY (SAU KHU COMMIT TRANSACTION)
                var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
                if (company != null)
                {
                    // Chạy hàm background không làm treo Response HTTP
                    _ = ProcessPendingAiAnalysesForEmployerAsync(company.MaCongTy, _serviceProvider);
                }

                // GỬI EMAIL BIÊN LAI
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

                    await _emailService.SendEmailAsync(user.Email, $"[JobsNow] Kích hoạt {package.TenGoi} thành công", emailBody);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LỖI GỬI EMAIL]: {ex.Message}");
                }

                return Ok(new
                {
                    message = "Đăng ký gói thành công! Quyền lợi và cỗ máy AI đã được kích hoạt.",
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

        // 🌟 3. HÀM XỬ LÝ BACKGROUND CHẠY BÙ PHÂN TÍCH AI CHO CÁC ĐƠN CŨ
        private async Task ProcessPendingAiAnalysesForEmployerAsync(int maCongTy, IServiceProvider serviceProvider)
        {
            _ = Task.Run(async () =>
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<JobPortalDbContext>();
                var aiService = scope.ServiceProvider.GetRequiredService<IAiAnalysisService>();

                try
                {
                    // 🌟 CẬP NHẬT: Quét cả những đơn chưa phân tích HOẶC có record rỗng (ThongTinHoSoTrichXuatJson == "{}")
                    var unanalyzedAppIds = await context.DonUngTuyens
                        .Include(d => d.MaViTriNavigation)
                            .ThenInclude(v => v.MaTinNavigation)
                        .Where(d => d.MaViTriNavigation.MaTinNavigation.MaCongTy == maCongTy
                                 && d.MaViTriNavigation.MaTinNavigation.TrangThai == 1
                                 && (d.ChiTietPhanTichAi == null
                                  || d.ChiTietPhanTichAi.DiemMatchingTong == 0
                                  || d.ChiTietPhanTichAi.ThongTinHoSoTrichXuatJson == "{}"))
                        .Select(d => d.MaDon)
                        .ToListAsync();

                    foreach (var maDon in unanalyzedAppIds)
                    {
                        try
                        {
                            await aiService.AnalyzeApplicationAsync(maDon);
                            await Task.Delay(500); // Tránh rate limit
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AI Retroactive Error] MaDon {maDon}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI Retroactive General Error]: {ex.Message}");
                }
            });
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
                    TenGoi = g.MaGoiNavigation != null ? g.MaGoiNavigation.TenGoi : "Nạp tiền"
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

            var latestTx = await _context.GiaoDiches
                .Include(g => g.MaGoiNavigation)
                .Where(g => g.MaUser == maUser && g.LoaiGiaoDich == 2 && g.TrangThai == true)
                .OrderByDescending(g => g.NgayGd)
                .FirstOrDefaultAsync();

            string tenGoi = "Miễn phí";
            DateTime? ngayMua = null;

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