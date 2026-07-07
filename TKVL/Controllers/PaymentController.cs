using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TKVL.DTOs.Payment;
using TKVL.Models;
using TKVL.Services;
using TKVL.Utils;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly JobPortalDbContext _context;
        private readonly MomoConfig _config;
        private readonly IEmailService _emailService; // Tích hợp dịch vụ email gửi biên lai tự động

        public PaymentController(IPaymentService paymentService, JobPortalDbContext context, IOptions<MomoConfig> config, IEmailService emailService)
        {
            _paymentService = paymentService;
            _context = context;
            _config = config.Value;
            _emailService = emailService;
        }

        // Cập nhật endpoint: Nhận thêm mã gói dịch vụ mua kèm (nếu có)
        [HttpPost("create")]
        public async Task<IActionResult> CreatePaymentUrl(int maUser, decimal soTien, int? maGoi = null)
        {
            // Truyền maGoi xuống tầng Service để đóng gói vào chuỗi extraData gửi sang MoMo
            var response = await _paymentService.CreatePaymentAsync(maUser, soTien, maGoi);
            if (response.ResultCode == 0 && !string.IsNullOrEmpty(response.PayUrl))
            {
                return Ok(new { url = response.PayUrl });
            }
            return BadRequest(response.Message);
        }

        [HttpPost("MomoNotify")]
        public async Task<IActionResult> MomoNotify([FromBody] MomoNotifyRequest requestData)
        {
            try
            {
                string rawHash = $"accessKey={_config.AccessKey}&amount={requestData.amount}&extraData={requestData.extraData}&message={requestData.message}&orderId={requestData.orderId}&orderInfo={requestData.orderInfo}&orderType={requestData.orderType}&partnerCode={requestData.partnerCode}&payType={requestData.payType}&requestId={requestData.requestId}&responseTime={requestData.responseTime}&resultCode={requestData.resultCode}&transId={requestData.transId}";

                string signature = HashHelper.HmacSHA256(rawHash, _config.SecretKey);

                if (signature != requestData.signature)
                {
                    return BadRequest(new { message = "Sai chữ ký bảo mật!" });
                }

                // Giao dịch thành công phía MoMo
                if (requestData.resultCode == 0)
                {
                    // 1. Giải mã chuỗi extraData nhận về từ MoMo
                    byte[] extraDataBytes = Convert.FromBase64String(requestData.extraData);
                    string decodedStr = Encoding.UTF8.GetString(extraDataBytes);

                    int maUser;
                    int? maGoi = null;

                    // Phân tách chuỗi cấu trúc dạng "maUser|maGoi"
                    if (decodedStr.Contains("|"))
                    {
                        var parts = decodedStr.Split('|');
                        maUser = int.Parse(parts[0]);
                        if (!string.IsNullOrEmpty(parts[1]) && int.TryParse(parts[1], out int parsedMaGoi))
                        {
                            maGoi = parsedMaGoi;
                        }
                    }
                    else
                    {
                        maUser = int.Parse(decodedStr);
                    }

                    // Sử dụng Transaction bảo đảm tính toàn vẹn dữ liệu (ACID)
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);
                    if (user != null)
                    {
                        // Bước A: Cộng tiền vào ví điện tử của User từ giao dịch nạp tiền thành công
                        user.SoDuVi += requestData.amount;

                        var giaoDichNap = new GiaoDich
                        {
                            MaUser = maUser,
                            LoaiGiaoDich = 1, // 1: Nạp tiền
                            SoTien = requestData.amount,
                            PhuongThuc = "MoMo",
                            MaGiaoDichDoiTac = requestData.transId.ToString(),
                            NgayGd = DateTime.Now,
                            TrangThai = true
                        };
                        _context.GiaoDiches.Add(giaoDichNap);
                        await _context.SaveChangesAsync();

                        // Bước B: Xử lý TỰ ĐỘNG MUA GÓI DỊCH VỤ nếu có maGoi đi kèm
                        if (maGoi.HasValue)
                        {
                            var package = await _context.GoiDichVus.FirstOrDefaultAsync(g => g.MaGoi == maGoi.Value);
                            if (package != null)
                            {
                                decimal giaThucTe = (package.GiaKhuyenMai.HasValue && package.GiaKhuyenMai > 0)
                                                    ? package.GiaKhuyenMai.Value
                                                    : package.GiaTien;

                                // Đảm bảo số dư sau khi vừa nạp xong phải lớn hơn hoặc bằng giá tiền gói
                                if (user.SoDuVi >= giaThucTe)
                                {
                                    // 1. Thực hiện trừ tiền ví hệ thống ngầm
                                    user.SoDuVi -= giaThucTe;

                                    // 2. Cộng dồn lượt xem CV
                                    user.LuotXemCvConLai += package.SoLuotXemCv;

                                    // 3. Tính toán chu kỳ hạn sử dụng dịch vụ mới
                                    DateTime ngayBatDau = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi > DateTime.Now
                                                          ? user.NgayHetHanGoi.Value
                                                          : DateTime.Now;

                                    switch (package.LoaiGoi)
                                    {
                                        case 1:
                                            user.NgayHetHanGoi = ngayBatDau.AddDays(package.DonViThoiGian ?? 0);
                                            break;
                                        case 2:
                                            user.NgayHetHanGoi = ngayBatDau.AddMonths(package.DonViThoiGian ?? 0);
                                            break;
                                        case 3:
                                            user.NgayHetHanGoi = ngayBatDau.AddYears(package.DonViThoiGian ?? 0);
                                            break;
                                        default:
                                            user.NgayHetHanGoi = ngayBatDau.AddDays(package.DonViThoiGian ?? 0);
                                            break;
                                    }

                                    // 4. Ghi nhận log giao dịch mua gói dịch vụ tự động
                                    var giaoDichMuaGoi = new GiaoDich
                                    {
                                        MaUser = maUser,
                                        MaGoi = package.MaGoi,
                                        LoaiGiaoDich = 2, // 2: Mua gói dịch vụ
                                        SoTien = giaThucTe,
                                        PhuongThuc = "Ví nội bộ (Tự động sau nạp)",
                                        NgayGd = DateTime.Now,
                                        TrangThai = true
                                    };
                                    _context.GiaoDiches.Add(giaoDichMuaGoi);
                                    await _context.SaveChangesAsync();

                                    // 5. Xác nhận lưu vết toàn bộ chuỗi hành động thành công
                                    await transaction.CommitAsync();

                                    // 6. Gửi Email thông báo biên lai điện tử tự động
                                    try
                                    {
                                        string emailBody = $@"
                                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; padding: 20px; border-radius: 8px;'>
                                            <div style='text-align: center; border-bottom: 2px solid #D82D8B; padding-bottom: 15px; margin-bottom: 20px;'>
                                                <h2 style='color: #D82D8B; margin: 0;'>BIÊN LAI ĐIỆN TỬ (TỰ ĐỘNG KÍCH HOẠT)</h2>
                                                <p style='color: #666; margin: 5px 0 0 0;'>JobsNow - Hệ thống Tuyển dụng Chuyên nghiệp</p>
                                            </div>
                                            <p>Xin chào <b>{user.HoTen}</b>,</p>
                                            <p>Hệ thống đã nhận được tiền từ MoMo và thực hiện **kích hoạt gói dịch vụ tự động** thành công. Chi tiết giao dịch:</p>
                                            <table style='width: 100%; border-collapse: collapse; margin: 20px 0; background-color: #f9f9f9;'>
                                                <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Mã giao dịch hệ thống:</b></td><td style='padding: 10px; border: 1px solid #eee;'>#{giaoDichMuaGoi.MaGd}</td></tr>
                                                <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Thời gian kích hoạt:</b></td><td style='padding: 10px; border: 1px solid #eee;'>{giaoDichMuaGoi.NgayGd.ToString("dd/MM/yyyy HH:mm")}</td></tr>
                                                <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Gói dịch vụ kích hoạt:</b></td><td style='padding: 10px; border: 1px solid #eee; color: #0056b3; font-weight: bold;'>{package.TenGoi}</td></tr>
                                                <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Số tiền thanh toán:</b></td><td style='padding: 10px; border: 1px solid #eee;'>{giaThucTe.ToString("N0")} VNĐ</td></tr>
                                                <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Trạng thái đơn hàng:</b></td><td style='padding: 10px; border: 1px solid #eee; color: #389e0d; font-weight: bold;'>Đã hoàn tất tự động</td></tr>
                                            </table>
                                            <div style='background-color: #fffbe6; border-left: 4px solid #faad14; padding: 12px; margin-top: 20px;'>
                                                <p style='margin: 0; font-size: 16px;'>⏳ <b>Hạn sử dụng gói dịch vụ mới của bạn:</b> <span style='color: #cf1322; font-weight: bold;'>{user.NgayHetHanGoi?.ToString("dd/MM/yyyy")}</span></p>
                                            </div>
                                            <p style='color: #8c8c8c; font-size: 13px; text-align: center; margin-top: 30px; border-top: 1px solid #e0e0e0; padding-top: 15px;'>Đây là email xác nhận giao dịch tự động từ hệ thống.</p>
                                        </div>";

                                        await _emailService.SendEmailAsync(user.Email, $"[JobsNow] Tự động kích hoạt {package.TenGoi} thành công", emailBody);
                                    }
                                    catch (Exception emailEx)
                                    {
                                        Console.WriteLine($"[LỖI GỬI EMAIL TỰ ĐỘNG MUA GÓI]: {emailEx.Message}");
                                    }

                                    return NoContent();
                                }
                            }
                        }

                        // Nếu giao dịch không mua kèm gói dịch vụ, thực hiện commit quy trình nạp tiền thông thường
                        await transaction.CommitAsync();
                    }
                }

                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI IPN MOMO]: {ex.Message}");
                return BadRequest();
            }
        }

        [HttpGet("PaymentCallBack")]
        public IActionResult PaymentCallBack([FromQuery] string resultCode, [FromQuery] string orderId)
        {
            if (resultCode == "0")
            {
                return Redirect($"http://localhost:5173/payment-success?orderId={orderId}");
            }
            return Redirect($"http://localhost:5173/payment-failed?orderId={orderId}");
        }
    }
}