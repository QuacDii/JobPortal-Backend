using System.Collections.Generic;

namespace TKVL.Dtos
{
    // DTO Trả về dữ liệu mẫu CV
    public class MauCvDto
    {
        public int Id { get; set; }
        public int MaMau { get; set; }
        public string TenMau { get; set; } = string.Empty;
        public string? Title { get; set; }
        public string? MoTa { get; set; }
        public string? AnhThumbnail { get; set; }
        public string? Image { get; set; }
        public bool IsATS { get; set; }
        public bool IsVip { get; set; }
        public bool TrangThai { get; set; }
        public string NgonNgu { get; set; } = "VI";
        public string? Tags { get; set; }
        public string? DanhSachMau { get; set; }
        public List<string> Categories { get; set; } = new();
        public List<int> CategoryIds { get; set; } = new();
        public string? DuLieuMau { get; set; }
        public string? LayoutJson { get; set; }
    }
}