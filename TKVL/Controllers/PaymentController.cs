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

        // Đã sửa lại kiểu dữ liệu đầu vào là MomoNotifyRequest
        [HttpPost("MomoNotify")]
        public async Task<IActionResult> MomoNotify([FromBody] MomoNotifyRequest requestData)
        {
            try
            {
                // Chuỗi rawHash phải được nối đúng thứ tự Alphabet như MoMo yêu cầu
                string rawHash = $"accessKey={_config.AccessKey}&amount={requestData.amount}&extraData={requestData.extraData}&message={requestData.message}&orderId={requestData.orderId}&orderInfo={requestData.orderInfo}&orderType={requestData.orderType}&partnerCode={requestData.partnerCode}&payType={requestData.payType}&requestId={requestData.requestId}&responseTime={requestData.responseTime}&resultCode={requestData.resultCode}&transId={requestData.transId}";

                string signature = HashHelper.HmacSHA256(rawHash, _config.SecretKey);

                if (signature != requestData.signature)
                {
                    return BadRequest(new { message = "Sai chữ ký bảo mật!" });
                }

                // Giao dịch thành công (resultCode == 0)
                if (requestData.resultCode == 0)
                {
                    byte[] extraDataBytes = Convert.FromBase64String(requestData.extraData);
                    int maUser = int.Parse(Encoding.UTF8.GetString(extraDataBytes));

                    // Dùng Transaction để cập nhật DB
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);
                    if (user != null)
                    {
                        user.SoDuVi += requestData.amount; // Cộng tiền

                        var giaoDich = new GiaoDich
                        {
                            MaUser = maUser,
                            LoaiGiaoDich = 1, // 1: Nạp tiền
                            SoTien = requestData.amount,
                            PhuongThuc = "MoMo",
                            MaGiaoDichDoiTac = requestData.transId.ToString(),
                            NgayGd = DateTime.Now,
                            TrangThai = true
                        };

                        _context.GiaoDiches.Add(giaoDich);
                        await _context.SaveChangesAsync();
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

        // Đã sửa lại port thành 5173 khớp với ReactJS của bạn
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