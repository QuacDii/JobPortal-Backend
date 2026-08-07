using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

namespace TKVL.Dtos
{
    // 1. DTO Thêm mới mẫu CV
    public class MauCvCreateRequest
    {
        public string TenMau { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public string? AnhThumbnail { get; set; }
        public IFormFile? FileThumbnail { get; set; }
        public bool IsATS { get; set; }
        public bool IsVip { get; set; }
        public bool TrangThai { get; set; } = true;
        public string NgonNgu { get; set; } = "VI";
        public string? Tags { get; set; }
        public string? DuLieuMau { get; set; }
        public string? LayoutJson { get; set; }
        public string? DanhSachMau { get; set; }
        public List<int>? CategoryIds { get; set; }
    }

    // 2. DTO Cập nhật mẫu CV
    public class MauCvUpdateRequest : MauCvCreateRequest
    {
    }

    // 3. DTO Bật/Tắt trạng thái
    public class ToggleStatusRequest
    {
        public bool TrangThai { get; set; }
    }
}