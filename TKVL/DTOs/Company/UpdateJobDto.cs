namespace TKVL.DTOs
{
    public class UpdateJobRequestDto
    {
        public string TieuDeChienDich { get; set; } = null!;
        public DateTime NgayHetHan { get; set; }
        public List<UpdateJobPositionDto> DanhSachViTri { get; set; } = new();
    }

    public class UpdateJobPositionDto
    {
        public int? MaViTri { get; set; }
        public string TenViTri { get; set; } = null!;
        public string? CapBac { get; set; }
        public string? KinhNghiem { get; set; } // 🌟 Bổ sung KinhNghiem
        public int SoLuongTuyen { get; set; }
        public string Luong { get; set; } = null!;
        public string MoTaCongViec { get; set; } = null!;
        public string YeuCauUngVien { get; set; } = null!;
        public string QuyenLoi { get; set; } = null!;
        public int MaNganh { get; set; }
        public int MaPhuong { get; set; }
        public DateTime? NgayHetHan { get; set; }
        public List<string> DanhSachKyNang { get; set; } = new();
        public string? NganhNgheKhac { get; set; }
    }
}