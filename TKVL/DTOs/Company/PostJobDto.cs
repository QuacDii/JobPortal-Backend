namespace TKVL.DTOs
{
    // DTO cho bảng TinTuyenDung (Master)
    public class PostJobRequestDto
    {
        public string TieuDeChienDich { get; set; }
        public DateTime NgayHetHan { get; set; }

        // Danh sách các vị trí công việc cần tuyển
        public List<JobPositionDto> DanhSachViTri { get; set; }
    }

    // DTO cho bảng ChiTietViTri (Detail)
    public class JobPositionDto
    {
        public string TenViTri { get; set; }
        public int SoLuongTuyen { get; set; }
        public string Luong { get; set; }
        public string MoTaCongViec { get; set; }
        public string YeuCauUngVien { get; set; }
        public string QuyenLoi { get; set; }

        public int MaNganh { get; set; }
        public int MaPhuong { get; set; }
        public List<string> DanhSachKyNang { get; set; }
        public string? NganhNgheKhac { get; set; }
    }
}