using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Json;
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
        private readonly IEmailService _emailService;

        public PaymentController(
            IPaymentService paymentService,
            JobPortalDbContext context,
            IOptions<MomoConfig> config,
            IEmailService emailService)
        {
            _paymentService = paymentService;
            _context = context;
            _config = config.Value;
            _emailService = emailService;
        }

        // 1. Tạo liên kết thanh toán MoMo
        [HttpPost("create")]
        public async Task<IActionResult> CreatePaymentUrl(int maUser, decimal soTien, int? maGoi = null)
        {
            try
            {
                var response = await _paymentService.CreatePaymentAsync(maUser, soTien, maGoi);
                if (response != null && response.ResultCode == 0 && !string.IsNullOrEmpty(response.PayUrl))
                {
                    return Ok(new { url = response.PayUrl });
                }
                return BadRequest(new { success = false, message = response?.Message ?? "Tạo liên kết thanh toán MoMo thất bại!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tạo giao dịch MoMo!", error = ex.Message });
            }
        }

        // 2. Webhook IPN từ MoMo
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

                if (requestData.resultCode == 0)
                {
                    byte[] extraDataBytes = Convert.FromBase64String(requestData.extraData);
                    string decodedStr = Encoding.UTF8.GetString(extraDataBytes);
                    int maUser;
                    int? maGoi = null;

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

                    await ProcessPaymentSuccessAsync(maUser, requestData.amount, requestData.transId.ToString(), maGoi, "MoMo IPN");
                }
                return NoContent();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI IPN MOMO]: {ex.Message}");
                return BadRequest();
            }
        }

        // 3. Callback chuyển hướng sau thanh toán
        [HttpGet("PaymentCallBack")]
        public IActionResult PaymentCallBack([FromQuery] string resultCode, [FromQuery] string orderId)
        {
            if (resultCode == "0")
            {
                return Redirect($"http://localhost:5173/payment-success?orderId={orderId}");
            }
            return Redirect($"http://localhost:5173/payment-failed?orderId={orderId}");
        }

        // 4. Polling chủ động tra cứu trạng thái giao dịch
        [HttpGet("check-status")]
        public async Task<IActionResult> CheckPaymentStatus([FromQuery] string orderId, [FromQuery] int maUser, [FromQuery] int? maGoi = null)
        {
            try
            {
                bool isAlreadyProcessed = await _context.GiaoDiches
                    .AnyAsync(g => g.MaGiaoDichDoiTac == orderId && g.TrangThai == true);

                if (isAlreadyProcessed)
                {
                    return Ok(new { success = true, isPaid = true, message = "Giao dịch đã được ghi nhận!" });
                }

                string requestId = DateTime.Now.Ticks.ToString();
                string rawHash = $"accessKey={_config.AccessKey}&orderId={orderId}&partnerCode={_config.PartnerCode}&requestId={requestId}";
                string signature = HashHelper.HmacSHA256(rawHash, _config.SecretKey);

                var requestBody = new
                {
                    partnerCode = _config.PartnerCode,
                    requestId = requestId,
                    orderId = orderId,
                    signature = signature,
                    lang = "vi"
                };

                using var client = new HttpClient();
                var response = await client.PostAsJsonAsync("https://test-payment.momo.vn/v2/gateway/api/query", requestBody);
                var jsonResult = await response.Content.ReadFromJsonAsync<MomoQueryResponse>();

                if (jsonResult != null && jsonResult.resultCode == 0)
                {
                    await ProcessPaymentSuccessAsync(maUser, jsonResult.amount, orderId, maGoi, "MoMo Query");
                    return Ok(new { success = true, isPaid = true, message = "Thanh toán thành công!" });
                }

                return Ok(new { success = true, isPaid = false, message = "Chờ người dùng thanh toán..." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 5. Endpoint Fallback xử lý thủ công khi MoMo Sandbox treo
        [HttpPost("confirm-fallback")]
        public async Task<IActionResult> ConfirmFallback([FromBody] ConfirmFallbackRequest request)
        {
            try
            {
                if (request.ResultCode != "0")
                {
                    return BadRequest(new { success = false, message = "Giao dịch thanh toán không thành công!" });
                }

                bool isSuccess = await ProcessPaymentSuccessAsync(request.MaUser, request.Amount, request.OrderId, request.MaGoi, "MoMo (Fallback)");
                if (!isSuccess)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin người dùng!" });
                }

                return Ok(new { success = true, message = "Xác nhận giao dịch thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi xử lý fallback!", error = ex.Message });
            }
        }

        // ==========================================
        // HÀM DÙNG CHUNG: CỘNG TIỀN + MUA GÓI + GỬI EMAIL
        // ==========================================
        private async Task<bool> ProcessPaymentSuccessAsync(int maUser, decimal amount, string orderId, int? maGoi, string phuongThuc)
        {
            // 1. Chống cộng trùng tiền
            bool isAlreadyProcessed = await _context.GiaoDiches
                .AnyAsync(g => g.MaGiaoDichDoiTac == orderId && g.TrangThai == true);

            if (isAlreadyProcessed) return true;

            using var transaction = await _context.Database.BeginTransactionAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);
            if (user == null) return false;

            // 2. Cộng tiền vào Ví
            user.SoDuVi += amount;
            var giaoDichNap = new GiaoDich
            {
                MaUser = maUser,
                LoaiGiaoDich = 1, // 1: Nạp tiền
                SoTien = amount,
                PhuongThuc = phuongThuc,
                MaGiaoDichDoiTac = orderId,
                NgayGd = DateTime.Now,
                TrangThai = true
            };
            _context.GiaoDiches.Add(giaoDichNap);
            await _context.SaveChangesAsync();

            // 3. Xử lý Mua gói dịch vụ (Nếu có)
            GoiDichVu? package = null;
            if (maGoi.HasValue)
            {
                package = await _context.GoiDichVus.FirstOrDefaultAsync(g => g.MaGoi == maGoi.Value);
                if (package != null)
                {
                    decimal giaThucTe = (package.GiaKhuyenMai.HasValue && package.GiaKhuyenMai > 0)
                                        ? package.GiaKhuyenMai.Value
                                        : package.GiaTien;

                    if (user.SoDuVi >= giaThucTe)
                    {
                        user.SoDuVi -= giaThucTe;
                        user.LuotXemCvConLai += package.SoLuotXemCv;

                        DateTime ngayBatDau = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi > DateTime.Now
                                              ? user.NgayHetHanGoi.Value
                                              : DateTime.Now;

                        switch (package.LoaiGoi)
                        {
                            case 1: user.NgayHetHanGoi = ngayBatDau.AddDays(package.DonViThoiGian ?? 0); break;
                            case 2: user.NgayHetHanGoi = ngayBatDau.AddMonths(package.DonViThoiGian ?? 0); break;
                            case 3: user.NgayHetHanGoi = ngayBatDau.AddYears(package.DonViThoiGian ?? 0); break;
                            default: user.NgayHetHanGoi = ngayBatDau.AddDays(package.DonViThoiGian ?? 0); break;
                        }

                        _context.GiaoDiches.Add(new GiaoDich
                        {
                            MaUser = maUser,
                            MaGoi = package.MaGoi,
                            LoaiGiaoDich = 2, // 2: Mua gói
                            SoTien = giaThucTe,
                            PhuongThuc = "Ví nội bộ (Tự động)",
                            NgayGd = DateTime.Now,
                            TrangThai = true
                        });
                        await _context.SaveChangesAsync();
                    }
                }
            }

            await transaction.CommitAsync();

            // 4. GỬI EMAIL BIÊN LAI ĐIỆN TỬ SANG TRỌNG & TỰ ĐỘNG KHỞI TẠO NỘI DUNG
            try
            {
                string tenDichVu = package != null ? $"Kích hoạt {package.TenGoi}" : "Nạp tiền vào Ví điện tử TKVL";
                string moTaGiaoDich = package != null
                    ? "Hệ thống đã nhận được tiền thanh toán và thực hiện <b>kích hoạt gói dịch vụ tự động</b> thành công."
                    : "Hệ thống đã ghi nhận số tiền nạp thành công vào số dư Ví điện tử của bạn.";

                string expirationBlock = (package != null && user.NgayHetHanGoi.HasValue) ? $@"
                <div style='background-color: #fffbe6; border-left: 4px solid #faad14; padding: 12px; margin-top: 20px;'>
                    <p style='margin: 0; font-size: 15px;'>⏳ <b>Hạn sử dụng gói dịch vụ mới của bạn:</b> <span style='color: #cf1322; font-weight: bold;'>{user.NgayHetHanGoi.Value:dd/MM/yyyy}</span></p>
                </div>" : "";

                string emailBody = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e0e0e0; padding: 20px; border-radius: 8px;'>
                    <div style='text-align: center; border-bottom: 2px solid #D82D8B; padding-bottom: 15px; margin-bottom: 20px;'>
                        <h2 style='color: #D82D8B; margin: 0;'>BIÊN LAI ĐIỆN TỬ</h2>
                        <p style='color: #666; margin: 5px 0 0 0;'>JobsNow - Hệ thống Tuyển dụng Chuyên nghiệp</p>
                    </div>
                    <p>Xin chào <b>{user.HoTen}</b>,</p>
                    <p>{moTaGiaoDich} Chi tiết giao dịch:</p>
                    <table style='width: 100%; border-collapse: collapse; margin: 20px 0; background-color: #f9f9f9;'>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Mã đơn hàng:</b></td><td style='padding: 10px; border: 1px solid #eee;'>#{orderId}</td></tr>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Thời gian giao dịch:</b></td><td style='padding: 10px; border: 1px solid #eee;'>{DateTime.Now:dd/MM/yyyy HH:mm}</td></tr>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Nội dung thanh toán:</b></td><td style='padding: 10px; border: 1px solid #eee; color: #0056b3; font-weight: bold;'>{tenDichVu}</td></tr>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Số tiền giao dịch:</b></td><td style='padding: 10px; border: 1px solid #eee; color: #389e0d; font-weight: bold;'>{amount:N0} VNĐ</td></tr>
                        <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Trạng thái:</b></td><td style='padding: 10px; border: 1px solid #eee; color: #389e0d; font-weight: bold;'>Hoàn tất thành công</td></tr>
                    </table>
                    {expirationBlock}
                    <p style='color: #8c8c8c; font-size: 13px; text-align: center; margin-top: 30px; border-top: 1px solid #e0e0e0; padding-top: 15px;'>Đây là email xác nhận giao dịch tự động từ hệ thống JobsNow.</p>
                </div>";

                await _emailService.SendEmailAsync(user.Email, $"[JobsNow] Biên lai giao dịch #{orderId} thành công", emailBody);
            }
            catch (Exception emailEx)
            {
                Console.WriteLine($"[LỖI GỬI EMAIL BIÊN LAI]: {emailEx.Message}");
            }

            return true;
        }
    }

    public class MomoQueryResponse
    {
        public int resultCode { get; set; }
        public string message { get; set; }
        public decimal amount { get; set; }
    }

    public class ConfirmFallbackRequest
    {
        public int MaUser { get; set; }
        public decimal Amount { get; set; }
        public string OrderId { get; set; }
        public string ResultCode { get; set; }
        public int? MaGoi { get; set; }
    }
}