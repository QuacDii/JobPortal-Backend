using TKVL.DTOs.Payment;

namespace TKVL.Services
{
    public interface IPaymentService
    {
        Task<MomoCreatePaymentResponse> CreatePaymentAsync(int maUser, decimal soTien);
    }
}