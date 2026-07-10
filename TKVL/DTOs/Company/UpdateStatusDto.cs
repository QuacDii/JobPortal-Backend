public class UpdateStatusDto
{
    public int Status { get; set; }
    public string? GhiChu { get; set; }
    public string? ThoiGian { get; set; } // Thời gian hẹn phỏng vấn
    public string? DiaDiem { get; set; }  // Địa điểm phỏng vấn trực tiếp hoặc online
}