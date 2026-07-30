using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TKVL.DTOs.Payment;
using TKVL.Utils;

namespace TKVL.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly MomoConfig _config;
        private readonly HttpClient _httpClient;

        public PaymentService(IOptions<MomoConfig> config, HttpClient httpClient)
        {
            _config = config.Value;
            _httpClient = httpClient; // Tận dụng HttpClient đã được DI Container quản lý
        }

        public async Task<MomoCreatePaymentResponse> CreatePaymentAsync(int maUser, decimal soTien, int? maGoi)
        {
            // 1. Kiểm tra an toàn xem cấu hình MoMo đã được load đủ chưa
            string targetUrl = !string.IsNullOrEmpty(_config.MomoApiUrl) ? _config.MomoApiUrl : _config.PaymentUrl;
            if (string.IsNullOrEmpty(targetUrl))
            {
                throw new Exception("Chưa cấu hình MomoApiUrl/PaymentUrl trong appsettings.json!");
            }

            string orderId = DateTime.UtcNow.Ticks.ToString();
            string requestId = DateTime.UtcNow.Ticks.ToString();
            string orderInfo = $"Nap tien vao vi dien tu JobsNow - Ma GD: {orderId}";
            string amount = ((long)soTien).ToString(); // Ép kiểu sang long để tránh sai số tiền lớn

            // 2. Mã hóa extraData dạng Base64 (Chứa maUser và maGoi)
            string extraData = maGoi.HasValue
                ? Convert.ToBase64String(Encoding.UTF8.GetBytes($"{maUser}|{maGoi.Value}"))
                : Convert.ToBase64String(Encoding.UTF8.GetBytes(maUser.ToString()));

            // 3. Chuỗi rawHash để băm chữ ký SHA256
            string rawHash = $"accessKey={_config.AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={_config.NotifyUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={_config.PartnerCode}&redirectUrl={_config.ReturnUrl}&requestId={requestId}&requestType={_config.RequestType}";

            string signature = HashHelper.HmacSHA256(rawHash, _config.SecretKey);

            var requestData = new
            {
                partnerCode = _config.PartnerCode,
                requestId = requestId,
                amount = amount,
                orderId = orderId,
                orderInfo = orderInfo,
                redirectUrl = _config.ReturnUrl,
                ipnUrl = _config.NotifyUrl,
                requestType = _config.RequestType,
                extraData = extraData,
                lang = "vi",
                signature = signature
            };

            // 4. Gửi Request bằng _httpClient có sẵn (KHÔNG tự new HttpClient nữa)
            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(targetUrl, content);

            string responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"👉 [MOMO API RESPONSE]: {responseContent}"); // Log dữ liệu MoMo trả về

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"MoMo Gateway trả về lỗi HTTP {(int)response.StatusCode}: {responseContent}");
            }

            var result = JsonConvert.DeserializeObject<MomoCreatePaymentResponse>(responseContent);
            return result ?? new MomoCreatePaymentResponse();
        }
    }
}