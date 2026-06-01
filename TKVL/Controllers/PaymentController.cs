using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text;
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

        public PaymentController(IPaymentService paymentService, JobPortalDbContext context, IOptions<MomoConfig> config)
        {
            _paymentService = paymentService;
            _context = context;
            _config = config.Value;
        }

        // 1. GỌI TỪ FRONTEND ĐỂ TẠO URL QR CODE
        [HttpPost("create")]
        public async Task<IActionResult> CreatePaymentUrl(int maUser, decimal soTien)
        {
            var response = await _paymentService.CreatePaymentAsync(maUser, soTien);
            if (response.ResultCode == 0 && !string.IsNullOrEmpty(response.PayUrl))
            {
                return Ok(new { url = response.PayUrl });
            }
            return BadRequest(response.Message);
        }

        // 2. IPN/WEBHOOK: MOMO GỌI NGẦM VÀO ĐÂY ĐỂ XÁC NHẬN CỘNG TIỀN (Quan trọng nhất)
        [HttpPost("MomoNotify")]
        public async Task<IActionResult> MomoNotify([FromBody] Dictionary<string, string> requestData)
        {
            try
            {
                // Verify Signature để chống hacker gọi API giả mạo
                string rawHash = $"accessKey={_config.AccessKey}&amount={requestData["amount"]}&extraData={requestData["extraData"]}&message={requestData["message"]}&orderId={requestData["orderId"]}&orderInfo={requestData["orderInfo"]}&orderType={requestData["orderType"]}&partnerCode={requestData["partnerCode"]}&payType={requestData["payType"]}&requestId={requestData["requestId"]}&responseTime={requestData["responseTime"]}&resultCode={requestData["resultCode"]}&transId={requestData["transId"]}";

                string signature = HashHelper.HmacSHA256(rawHash, _config.SecretKey);

                if (signature != requestData["signature"])
                {
                    return BadRequest(new { message = "Sai chữ ký bảo mật!" });
                }

                // Giao dịch thành công
                if (requestData["resultCode"] == "0")
                {
                    // Lấy MaUser từ extraData (Giải mã Base64)
                    byte[] extraDataBytes = Convert.FromBase64String(requestData["extraData"]);
                    int maUser = int.Parse(Encoding.UTF8.GetString(extraDataBytes));
                    decimal amount = decimal.Parse(requestData["amount"]);

                    // Dùng Transaction để đảm bảo toàn vẹn dữ liệu ACID
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);
                    if (user != null)
                    {
                        // 1. Cộng tiền
                        user.SoDuVi += amount;

                        // 2. Lưu lịch sử giao dịch (Khớp với ERD của bạn)
                        var giaoDich = new GiaoDich
                        {
                            MaUser = maUser,
                            LoaiGiaoDich = 1, // 1: Nạp tiền
                            SoTien = amount,
                            PhuongThuc = "MoMo",
                            MaGiaoDichDoiTac = requestData["transId"], // Lưu mã GD của MoMo để đối soát
                            NgayGd = DateTime.Now,
                            TrangThai = true // Thành công
                        };

                        _context.GiaoDiches.Add(giaoDich);
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();
                    }
                }

                // MoMo yêu cầu trả về status 204 NoContent để biết Server mình đã nhận được thông báo
                return NoContent();
            }
            catch (Exception ex)
            {
                return BadRequest();
            }
        }

        // 3. RETURN URL: SAU KHI QUÉT QR, TRÌNH DUYỆT SẼ NHẢY VỀ ĐÂY
        [HttpGet("PaymentCallBack")]
        public IActionResult PaymentCallBack([FromQuery] string resultCode, [FromQuery] string orderId)
        {
            if (resultCode == "0")
            {
                // Chuyển hướng về ReactJS trang nạp tiền thành công
                return Redirect($"http://localhost:3000/payment-success?orderId={orderId}");
            }
            // Thất bại
            return Redirect($"http://localhost:3000/payment-failed?orderId={orderId}");
        }
    }
}