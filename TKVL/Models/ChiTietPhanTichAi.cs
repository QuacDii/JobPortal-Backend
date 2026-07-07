using System;

namespace TKVL.Models;

public partial class ChiTietPhanTichAi
{
    public int MaPhanTich { get; set; } // Khóa chính tự tăng
    public int MaDon { get; set; } // Khóa ngoại trỏ sang DonUngTuyen

    // Các điểm số phục vụ vẽ thanh Progress Bar (%) trên giao diện
    public int DiemMatchingTong { get; set; }
    public int DiemKyNang { get; set; }
    public int DiemKinhNghiem { get; set; }
    public int DiemLinhVuc { get; set; }
    public int DiemCapBac { get; set; }

    // Dữ liệu văn bản hiển thị Điểm mạnh / Điểm thiếu sót
    public string? DiemManhTieuBieu { get; set; }
    public string? DiemConThieu { get; set; }

    // Lưu chuỗi JSON trích xuất gốc (Họ tên, email, sđt, học vấn...) để đổ vào Form bên phải
    public string? ThongTinHoSoTrichXuatJson { get; set; }

    // Thuộc tính điều hướng quan hệ (Navigation Property)
    public virtual DonUngTuyen MaDonNavigation { get; set; } = null!;
}