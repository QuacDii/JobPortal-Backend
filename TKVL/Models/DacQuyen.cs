using System.Collections.Generic;

namespace TKVL.Models;

public partial class DacQuyen
{
    public int MaDacQuyen { get; set; }

    public string MaCode { get; set; } = null!; // Ví dụ: NTD_VIP_JOB, UV_AI_REVIEW, NTD_UNLOCK_CV

    public string TenDacQuyen { get; set; } = null!; // Mô tả hiển thị

    public byte DoiTuongSuDung { get; set; } // 1: NTD, 2: Ứng viên

    public string? MoTa { get; set; }

    public virtual ICollection<GoiDichVu_DacQuyen> GoiDichVu_DacQuyens { get; set; } = new List<GoiDichVu_DacQuyen>();
    public virtual ICollection<UserDacQuyen> UserDacQuyens { get; set; } = new List<UserDacQuyen>();
}