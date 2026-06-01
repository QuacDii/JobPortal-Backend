using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;
using TKVL.DTOs.Payment;
using TKVL.Utils;

namespace TKVL.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly MomoConfig _config;

        public PaymentService(IOptions<MomoConfig> config)
        {
            _config = config.Value;
        }

        public async Task<MomoCreatePaymentResponse> CreatePaymentAsync(int maUser, decimal soTien)
        {
            string orderId = DateTime.UtcNow.Ticks.ToString();
            string requestId = DateTime.UtcNow.Ticks.ToString();
            string orderInfo = $"Nap tien vao vi TKVL - User: {maUser}";
            string amount = ((int)soTien).ToString();

            // MoMo yêu cầu extraData dạng Base64. Ta nhét maUser vào đây để lấy ra lúc xử lý IPN.
            string extraData = Convert.ToBase64String(Encoding.UTF8.GetBytes(maUser.ToString()));

            // Format chuỗi chuẩn để băm chữ ký
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

            using HttpClient client = new HttpClient();
            var content = new StringContent(JsonConvert.SerializeObject(requestData), Encoding.UTF8, "application/json");

            var response = await client.PostAsync(_config.MomoApiUrl, content);
            string responseContent = await response.Content.ReadAsStringAsync();

            var result = JsonConvert.DeserializeObject<MomoCreatePaymentResponse>(responseContent);
            return result ?? new MomoCreatePaymentResponse();
        }
    }
}