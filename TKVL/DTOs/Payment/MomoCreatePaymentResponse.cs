namespace TKVL.DTOs.Payment
{
    public class MomoCreatePaymentResponse
    {
        public string PayUrl { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int ResultCode { get; set; }
        public int? MaGoi { get; set; } = null;
    }
}