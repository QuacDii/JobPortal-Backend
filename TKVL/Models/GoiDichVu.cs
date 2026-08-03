using System;
using System.Collections.Generic;

namespace TKVL.Models;

public partial class GoiDichVu
{
    public int MaGoi { get; set; }

    public string TenGoi { get; set; } = null!;

    public byte LoaiGoi { get; set; } // 1: Ngày, 2: Tháng, 3: Năm

    public decimal GiaTien { get; set; }

    public int? DonViThoiGian { get; set; }

    public decimal? GiaKhuyenMai { get; set; }

    // 🌟 BỔ SUNG
    public byte DoiTuongSuDung { get; set; } = 1; // 1: NTD, 2: Ứng viên

    public bool TrangThai { get; set; } = true; // true: Đang bán, false: Tạm ẩn

    public virtual ICollection<GiaoDich> GiaoDiches { get; set; } = new List<GiaoDich>();

    // 🌟 QUAN HỆ 1 - N VỚI BẢNG TRUNG GIANG ĐẶC QUYỀN
    public virtual ICollection<GoiDichVu_DacQuyen> GoiDichVu_DacQuyens { get; set; } = new List<GoiDichVu_DacQuyen>();
}