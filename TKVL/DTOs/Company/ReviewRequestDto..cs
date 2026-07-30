namespace TKVL.DTOs.Company
{
    public class ReviewCompanyDto
    {
        public string? ActionType { get; set; } // "APPROVE", "REJECT", "REQUEST_ADDITION"
        public string? YeuCauBoSung { get; set; }
        public bool? IsApproved { get; set; } // Giữ lại để hỗ trợ tương thích ngược nếu cần
    }
}
