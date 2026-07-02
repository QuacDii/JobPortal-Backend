namespace TKVL.DTOs.Company
{
    public class CompanyProfileDto
    {
        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string QuyMo { get; set; }
        public string DiaChi { get; set; }
        public string MoTa { get; set; }
        public IFormFile? LogoFile { get; set; }
    }
}
