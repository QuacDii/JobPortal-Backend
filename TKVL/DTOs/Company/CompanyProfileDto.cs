// Trong File: DTOs/Company/CompanyProfileDto.cs
using Microsoft.AspNetCore.Http;
using System;
using System.ComponentModel.DataAnnotations;

namespace TKVL.DTOs.Company
{
    public class CompanyProfileDto
    {
        [Required(ErrorMessage = "Tên công ty không được để trống!")]
        public string TenCongTy { get; set; } = string.Empty;

        [Required(ErrorMessage = "Mã số thuế không được để trống!")]
        public string MaSoThue { get; set; } = string.Empty;

        public string QuyMo { get; set; } = string.Empty;
        public string DiaChi { get; set; } = string.Empty;
        public string? MoTa { get; set; }
        public string? MauEmailInterview { get; set; }

        // Bổ sung thông tin pháp lý
        public string? NguoiDaiDienPhapLuat { get; set; }
        public DateTime? NgayCapGiayPhep { get; set; }
        public string? NoiCapGiayPhep { get; set; }

        public string? ChuKyEmail { get; set; }
        // File upload
        public IFormFile? LogoFile { get; set; }
        public IFormFile? GiayPhepKinhDoanhMatTruocFile { get; set; }
        public IFormFile? GiayPhepKinhDoanhMatSauFile { get; set; }

        // Hỗ trợ thêm 1 File PDF GPKD tổng hợp (Nếu doanh nghiệp nộp PDF)
        public IFormFile? GiayPhepPdfFile { get; set; }
    }
    public class CompanyPendingUpdateDto
    {
        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string GiayPhepKinhDoanhMatTruoc { get; set; }
        public string GiayPhepKinhDoanhMatSau { get; set; }
        public DateTime NgayYeuCau { get; set; } = DateTime.Now;
        public string QuyMo { get; set; }
        public string DiaChi { get; set; }
        public string MoTa { get; set; }
        public IFormFile? LogoFile { get; set; }
        public string? ChuKyEmail { get; set; }
        public string? MauEmailInterview { get; set; }
    }
    public class PendingCompanyListDto
    {
        public int MaCongTy { get; set; }
        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string DiaChi { get; set; }
        public string QuyMo { get; set; }
        public string NguoiDaiDien { get; set; }
        public string Email { get; set; }
        public string LoaiYeuCau { get; set; } // "NEW" (Đăng ký mới) hoặc "UPDATE" (Cập nhật thông tin)
        public bool TrangThaiHienTai { get; set; }
        public CompanyPendingUpdateDto ThongTinChoDuyet { get; set; } // Chứa thông tin mới nếu là loại UPDATE
    }
}