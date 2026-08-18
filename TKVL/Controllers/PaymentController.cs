using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
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
        private readonly MomoConfig _momoConfig;
        private readonly VnPayConfig _vnpayConfig;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> _paymentLocks = new();

        public PaymentController(
            IPaymentService paymentService,
            JobPortalDbContext context,
            IOptions<MomoConfig> momoConfig,
            IOptions<VnPayConfig> vnpayConfig,
            IEmailService emailService,
            IConfiguration config)
        {
            _paymentService = paymentService;
            _context = context;
            _momoConfig = momoConfig.Value;
            _vnpayConfig = vnpayConfig.Value;
            _config = config;
            _emailService = emailService;
        }

        // 1. Tạo liên kết thanh toán MoMo
        [HttpPost("create")]
        public async Task<IActionResult> CreatePaymentUrl([FromQuery] int maUser, decimal soTien, int? maGoi = null)
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
                string rawHash = $"accessKey={_momoConfig.AccessKey}&amount={requestData.amount}&extraData={requestData.extraData}&message={requestData.message}&orderId={requestData.orderId}&orderInfo={requestData.orderInfo}&orderType={requestData.orderType}&partnerCode={requestData.partnerCode}&payType={requestData.payType}&requestId={requestData.requestId}&responseTime={requestData.responseTime}&resultCode={requestData.resultCode}&transId={requestData.transId}";
                string signature = HashHelper.HmacSHA256(rawHash, _momoConfig.SecretKey);
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
                string rawHash = $"accessKey={_momoConfig.AccessKey}&orderId={orderId}&partnerCode={_momoConfig.PartnerCode}&requestId={requestId}";
                string signature = HashHelper.HmacSHA256(rawHash, _momoConfig.SecretKey);
                var requestBody = new
                {
                    partnerCode = _momoConfig.PartnerCode,
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
                bool isSuccess = await ProcessPaymentSuccessAsync(request.MaUser, request.Amount, request.OrderId, request.MaGoi, "MoMo");
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

        // =========================================================================
        // HÀM XỬ LÝ DÙNG CHUNG: CỘNG TIỀN VÍ + TỰ ĐỘNG MUA GÓI + GỬI BIÊN LAI EMAIL
        // =========================================================================
        private async Task<bool> ProcessPaymentSuccessAsync(int maUser, decimal amount, string orderId, int? maGoi, string phuongThuc)
        {
            var asyncLock = _paymentLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
            await asyncLock.WaitAsync();

            try
            {
                // 1. Chống xử lý trùng lặp đơn hàng
                bool isAlreadyProcessed = await _context.GiaoDiches
                    .AnyAsync(g => g.MaGiaoDichDoiTac == orderId && g.TrangThai == true);

                if (isAlreadyProcessed)
                {
                    return true;
                }

                using var transaction = await _context.Database.BeginTransactionAsync();

                var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);
                if (user == null) return false;

                // 2. CỘNG TIỀN NẠP BÙ VÀO VÍ & CẬP NHẬT GIAO DỊCH NẠP TIỀN
                user.SoDuVi += amount;

                var existingGiaoDich = await _context.GiaoDiches
                    .FirstOrDefaultAsync(g => g.MaGiaoDichDoiTac == orderId);

                if (existingGiaoDich != null)
                {
                    existingGiaoDich.TrangThai = true;
                    existingGiaoDich.PhuongThuc = phuongThuc;
                    existingGiaoDich.LoaiGiaoDich = 1; // 🌟 Đảm bảo là 1: Nạp tiền
                    existingGiaoDich.MaGoi = null;     // 🌟 Gỡ MaGoi để không bị tính là giao dịch mua gói
                }
                else
                {
                    _context.GiaoDiches.Add(new GiaoDich
                    {
                        MaUser = maUser,
                        LoaiGiaoDich = 1, // 🌟 Nạp tiền
                        SoTien = amount,
                        PhuongThuc = phuongThuc,
                        MaGiaoDichDoiTac = orderId,
                        NgayGd = DateTime.Now,
                        TrangThai = true
                    });
                }

                await _context.SaveChangesAsync();

                // 3. XỬ LÝ TỰ ĐỘNG MUA GÓI DỊCH VỤ (NẾU CÓ CHỌN GÓI)
                GoiDichVu? package = null;
                if (maGoi.HasValue)
                {
                    package = await _context.GoiDichVus
                        .Include(g => g.GoiDichVu_DacQuyens)
                            .ThenInclude(gd => gd.DacQuyen)
                        .FirstOrDefaultAsync(g => g.MaGoi == maGoi.Value);

                    if (package != null)
                    {
                        decimal giaThucTe = package.GiaKhuyenMai ?? package.GiaTien;

                        if (user.SoDuVi >= giaThucTe)
                        {
                            // Trừ tiền mua gói từ ví
                            user.SoDuVi -= giaThucTe;

                            // Cộng lượt mở khóa CV (nếu có đặc quyền)
                            var dacQuyenXemCv = package.GoiDichVu_DacQuyens
                                .FirstOrDefault(dq => dq.DacQuyen != null && dq.DacQuyen.MaCode == "NTD_UNLOCK_CV");

                            if (dacQuyenXemCv != null && dacQuyenXemCv.SoLuong.HasValue)
                            {
                                user.LuotXemCvConLai = (user.LuotXemCvConLai) + dacQuyenXemCv.SoLuong.Value;
                            }

                            // Tính thời hạn gói mới
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

                            // Cập nhật các đặc quyền vào bảng UserDacQuyens
                            foreach (var item in package.GoiDichVu_DacQuyens)
                            {
                                var userDacQuyen = await _context.UserDacQuyens
                                    .FirstOrDefaultAsync(ud => ud.MaUser == maUser && ud.MaDacQuyen == item.MaDacQuyen);

                                if (userDacQuyen != null)
                                {
                                    if (item.SoLuong.HasValue)
                                        userDacQuyen.SoLuotConLai = (userDacQuyen.SoLuotConLai ?? 0) + item.SoLuong.Value;
                                    userDacQuyen.NgayHetHan = user.NgayHetHanGoi.Value;
                                }
                                else
                                {
                                    _context.UserDacQuyens.Add(new UserDacQuyen
                                    {
                                        MaUser = maUser,
                                        MaDacQuyen = item.MaDacQuyen,
                                        SoLuotConLai = item.SoLuong,
                                        NgayHetHan = user.NgayHetHanGoi.Value
                                    });
                                }
                            }

                            // 🌟 LƯU GIAO DỊCH MUA GÓI (LoaiGiaoDich = 2)
                            _context.GiaoDiches.Add(new GiaoDich
                            {
                                MaUser = maUser,
                                MaGoi = package.MaGoi,
                                LoaiGiaoDich = 2, // 🌟 Giao dịch Mua gói
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

                // 4. GỬI EMAIL BIÊN LAI ĐIỆN TỬ
                try
                {
                    decimal giaTriGoi = package != null ? (package.GiaKhuyenMai ?? package.GiaTien) : amount;
                    decimal soTienTuVi = package != null ? Math.Max(0, giaTriGoi - amount) : 0;

                    string tenDichVu = package != null ? $"Kích hoạt {package.TenGoi}" : "Nạp tiền vào Ví điện tử JobsNow";
                    string moTaGiaoDich = package != null
                        ? "Hệ thống đã nhận được tiền thanh toán và thực hiện <b>kích hoạt gói dịch vụ tự động</b> thành công."
                        : "Hệ thống đã ghi nhận số tiền nạp thành công vào số dư Ví điện tử của bạn.";

                    string chiTietNguonTien = (package != null && soTienTuVi > 0)
                        ? $@"
                <tr>
                    <td style='padding: 10px; border: 1px solid #eee;'><b>Nguồn tiền thanh toán:</b></td>
                    <td style='padding: 10px; border: 1px solid #eee;'>
                        • Nạp mới qua {phuongThuc}: <b>{amount:N0} VNĐ</b><br/>
                        • Trừ số dư ví hiện có: <b>{soTienTuVi:N0} VNĐ</b>
                    </td>
                </tr>"
                        : $@"
                <tr>
                    <td style='padding: 10px; border: 1px solid #eee;'><b>Phương thức:</b></td>
                    <td style='padding: 10px; border: 1px solid #eee;'>{phuongThuc}</td>
                </tr>";

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
                    <tr><td style='padding: 10px; border: 1px solid #eee;'><b>Tổng giá trị dịch vụ:</b></td><td style='padding: 10px; border: 1px solid #eee; color: #389e0d; font-weight: bold;'>{giaTriGoi:N0} VNĐ</td></tr>
                    {chiTietNguonTien}
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
            finally
            {
                asyncLock.Release();
                _paymentLocks.TryRemove(orderId, out _);
            }
        }

        // =========================================================================
        // 1. API TẠO URL THANH TOÁN VNPAY & LƯU GIAO DỊCH CHỜ (NẠP TIỀN)
        // =========================================================================
        [HttpPost("create-vnpay-url")]
        public async Task<IActionResult> CreateVnPayUrl([FromBody] CreatePaymentVNPay dto)
        {
            var vnpay = new VnPayLibrary();
            var timeZoneById = TimeZoneInfo.FindSystemTimeZoneById(_config["TimeZone"] ?? "SE Asia Standard Time");
            var timeNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZoneById);

            string txnRef = DateTime.Now.Ticks.ToString();

            // 🌟 GIAO DỊCH QUA CỔNG THANH TOÁN LUÔN LÀ NẠP TIỀN (LoaiGiaoDich = 1)
            var giaoDich = new GiaoDich
            {
                MaUser = dto.MaUser,
                SoTien = dto.SoTien,
                MaGoi = dto.MaGoi > 0 ? dto.MaGoi : null, // Lưu tạm để Callback đọc được gói cần kích hoạt
                MaGiaoDichDoiTac = txnRef,
                PhuongThuc = "VNPAY",
                LoaiGiaoDich = 1, // 🌟 LUÔN LÀ 1 (Nạp tiền), KHÔNG ĐỂ LÀ 2
                TrangThai = false,
                NgayGd = timeNow
            };

            _context.GiaoDiches.Add(giaoDich);
            await _context.SaveChangesAsync();

            vnpay.AddRequestData("vnp_Version", _vnpayConfig.Version);
            vnpay.AddRequestData("vnp_Command", _vnpayConfig.Command);
            vnpay.AddRequestData("vnp_TmnCode", _vnpayConfig.TmnCode);
            vnpay.AddRequestData("vnp_Amount", ((long)(dto.SoTien * 100)).ToString());
            vnpay.AddRequestData("vnp_CreateDate", timeNow.ToString("yyyyMMddHHmmss"));
            vnpay.AddRequestData("vnp_CurrCode", _vnpayConfig.CurrCode);
            vnpay.AddRequestData("vnp_IpAddr", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1");
            vnpay.AddRequestData("vnp_Locale", _vnpayConfig.Locale);
            vnpay.AddRequestData("vnp_OrderInfo", $"Thanh toan don hang #{txnRef}");
            vnpay.AddRequestData("vnp_OrderType", "other");
            vnpay.AddRequestData("vnp_ReturnUrl", _vnpayConfig.ReturnUrl);
            vnpay.AddRequestData("vnp_TxnRef", txnRef);

            string paymentUrl = vnpay.CreateRequestUrl(_vnpayConfig.BaseUrl, _vnpayConfig.HashSecret);

            return Ok(new { success = true, paymentUrl });
        }

        // 2. API XÁC THỰC KẾT QUẢ VNPAY & CỘNG TIỀN / MUA GÓI / GỬI MAIL
        [HttpGet("vnpay-callback")]
        public async Task<IActionResult> VnPayCallback()
        {
            var vnpay = new VnPayLibrary();
            foreach (var (key, value) in Request.Query)
            {
                if (!string.IsNullOrEmpty(key) && key.StartsWith("vnp_"))
                {
                    vnpay.AddResponseData(key, value.ToString());
                }
            }

            string vnp_SecureHash = Request.Query["vnp_SecureHash"];
            string vnp_ResponseCode = Request.Query["vnp_ResponseCode"];
            string vnp_TxnRef = Request.Query["vnp_TxnRef"];

            bool checkSignature = vnpay.ValidateSignature(vnp_SecureHash, _vnpayConfig.HashSecret);

            if (checkSignature)
            {
                var giaoDich = await _context.GiaoDiches.FirstOrDefaultAsync(g => g.MaGiaoDichDoiTac == vnp_TxnRef);
                if (giaoDich == null)
                {
                    return BadRequest(new { success = false, message = "Không tìm thấy giao dịch!" });
                }

                if (vnp_ResponseCode == "00")
                {
                    bool processSuccess = await ProcessPaymentSuccessAsync(
                        giaoDich.MaUser,
                        giaoDich.SoTien,
                        vnp_TxnRef,
                        giaoDich.MaGoi,
                        "VNPay"
                    );

                    if (processSuccess)
                    {
                        return Ok(new { success = true, orderId = vnp_TxnRef, message = "Thanh toán thành công!" });
                    }

                    return StatusCode(500, new { success = false, message = "Lỗi xử lý kích hoạt dịch vụ sau thanh toán." });
                }

                return Ok(new { success = false, orderId = vnp_TxnRef, responseCode = vnp_ResponseCode, message = "Giao dịch bị hủy hoặc thất bại." });
            }

            return BadRequest(new { success = false, message = "Chữ ký không hợp lệ!" });
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