using CloudinaryDotNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TKVL.DTOs.Company;
using TKVL.Models;
using TKVL.Services;
using static System.Net.Mime.MediaTypeNames;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "1")] // Phân quyền Role = 1 (Nhà tuyển dụng)
    public class EmployerController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IEmailService _emailService;

        public EmployerController(JobPortalDbContext context, ICloudinaryService cloudinaryService, IEmailService emailService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
            _emailService = emailService;
        }

        // ===================================================================
        // LUỒNG 1: QUẢN LÝ HỒ SƠ DOANH NGHIỆP
        // ===================================================================

        [HttpGet("company")]
        public async Task<IActionResult> GetCompanyProfile()
        {
            int maUser = GetCurrentUserId();
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
            if (company == null) return Ok(null);

            return Ok(new
            {
                tenCongTy = company.TenCongTy,
                maSoThue = company.MaSoThue,
                quyMo = company.QuyMo,
                diaChi = company.DiaChi,
                moTa = company.MoTa,
                trangThai = company.TrangThai,
                logo = company.Logo,
                giayPhepKinhDoanhMatTruoc = company.GiayPhepKinhDoanhMatTruoc,
                giayPhepKinhDoanhMatSau = company.GiayPhepKinhDoanhMatSau,
                yeuCauBoSung = company.YeuCauBoSung,
                mauEmailInterview = company.MauEmailInterview,
                chuKyEmail = company.ChuKyEmail, 
                duLieuChoDuyetJson = company.DuLieuChoDuyetJson
            });
        }

        [HttpPost("company")]
        public async Task<IActionResult> UpdateCompanyProfile([FromForm] CompanyProfileDto dto)
        {
            int maUser = GetCurrentUserId();
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
            bool isNew = (company == null);

            // 1. VALIDATION DỮ LIỆU ĐẦU VÀO
            if (string.IsNullOrWhiteSpace(dto.TenCongTy))
                return BadRequest(new { success = false, message = "Vui lòng nhập tên công ty!" });

            string targetMst = dto.MaSoThue?.Trim();
            if (string.IsNullOrWhiteSpace(targetMst))
                return BadRequest(new { success = false, message = "Vui lòng nhập mã số thuế!" });

            if (!Regex.IsMatch(targetMst, @"^\d+$"))
                return BadRequest(new { success = false, message = "Mã số thuế chỉ được nhập chữ số!" });

            if (!string.IsNullOrWhiteSpace(dto.QuyMo) && !Regex.IsMatch(dto.QuyMo.Trim(), @"^\d+$"))
                return BadRequest(new { success = false, message = "Quy mô nhân sự chỉ được nhập chữ số!" });

            if (string.IsNullOrWhiteSpace(dto.MoTa))
                return BadRequest(new { success = false, message = "Vui lòng nhập giới thiệu về công ty!" });

            // 2. KIỂM TRA TRÙNG MÃ SỐ THUẾ
            bool isMstExisted = await _context.CongTies.AnyAsync(c => c.MaSoThue == targetMst && c.MaUser != maUser);
            if (isMstExisted)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Mã số thuế '{targetMst}' đã được đăng ký bởi một doanh nghiệp khác trên hệ thống!"
                });
            }

            // 3. UPLOAD FILE MỚI VÀ VALIDATION
            string newLogoUrl = dto.LogoFile != null ? await _cloudinaryService.UploadLogoAsync(dto.LogoFile) : null;
            string newFrontUrl = dto.GiayPhepKinhDoanhMatTruocFile != null ? await _cloudinaryService.UploadGpkdAsync(dto.GiayPhepKinhDoanhMatTruocFile) : null;
            string newBackUrl = dto.GiayPhepKinhDoanhMatSauFile != null ? await _cloudinaryService.UploadGpkdAsync(dto.GiayPhepKinhDoanhMatSauFile) : null;

            string finalLogo = newLogoUrl ?? company?.Logo;
            if (string.IsNullOrEmpty(finalLogo))
                return BadRequest(new { success = false, message = "Vui lòng tải lên Logo của doanh nghiệp!" });

            string finalFront = newFrontUrl ?? company?.GiayPhepKinhDoanhMatTruoc;
            if (string.IsNullOrEmpty(finalFront))
                return BadRequest(new { success = false, message = "Vui lòng tải lên Giấy phép kinh doanh (Mặt trước / Bản chính)!" });

            string targetBack = newBackUrl ?? company?.GiayPhepKinhDoanhMatSau;

            if (isNew)
            {
                company = new CongTy { MaUser = maUser };
            }

            // ✨ CẬP NHẬT CÁC THÔNG TIN PHỤ (CÓ HIỆU LỰC NGAY)
            company.Logo = finalLogo;
            company.QuyMo = dto.QuyMo?.Trim();
            company.DiaChi = dto.DiaChi;
            company.MoTa = dto.MoTa;
            company.MauEmailInterview = dto.MauEmailInterview;
            company.ChuKyEmail = dto.ChuKyEmail; // ✨ BỔ SUNG LƯU CHỮ KÝ EMAIL

            bool hasLegalChanges = isNew ||
                company.TenCongTy != dto.TenCongTy ||
                company.MaSoThue != targetMst ||
                newFrontUrl != null ||
                newBackUrl != null;

            if (isNew || company.TrangThai == false)
            {
                company.TenCongTy = dto.TenCongTy;
                company.MaSoThue = targetMst;
                company.GiayPhepKinhDoanhMatTruoc = finalFront;
                company.GiayPhepKinhDoanhMatSau = targetBack;
                company.TrangThai = false;
                company.DuLieuChoDuyetJson = null;

                if (isNew) _context.CongTies.Add(company);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, isPending = true, message = "Lưu hồ sơ thành công! Yêu cầu đang chờ Ban quản trị phê duyệt." });
            }
            else if (hasLegalChanges)
            {
                var pendingData = new CompanyPendingUpdateDto
                {
                    TenCongTy = dto.TenCongTy,
                    MaSoThue = targetMst,
                    GiayPhepKinhDoanhMatTruoc = finalFront,
                    GiayPhepKinhDoanhMatSau = targetBack,
                    NgayYeuCau = DateTime.Now
                };

                company.DuLieuChoDuyetJson = JsonSerializer.Serialize(pendingData);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    isPendingUpdate = true,
                    message = "Thông tin giới thiệu/logo/chữ ký đã được cập nhật ngay! Yêu cầu thay đổi thông tin pháp lý đã gửi Ban quản trị thẩm định."
                });
            }

            company.YeuCauBoSung = null;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, isPending = false, message = "Cập nhật thông tin công ty thành công!" });
        }

        // ===================================================================
        // LUỒNG 2: QUẢN LÝ PHỄU ỨNG VIÊN & TIN TUYỂN DỤNG
        // ===================================================================

        // ===================================================================
        // API 1: KIỂM TRA QUYỀN SỬ DỤNG TÍNH NĂNG AI CỦA NHÀ TUYỂN DỤNG
        // ===================================================================
        [HttpGet("check-subscription")]
        public async Task<IActionResult> CheckSubscription()
        {
            int currentEmployerId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(currentEmployerId);
            if (user == null) return Unauthorized(new { isPremium = false });

            // Kiểm tra Hạn sử dụng gói (Chấp nhận sai số múi giờ)
            bool isNotExpired = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value.Date >= DateTime.Now.Date;

            bool hasAiFeature = false;
            if (isNotExpired)
            {
                hasAiFeature = await _context.GiaoDiches
                    .Include(g => g.MaGoiNavigation)
                    .AnyAsync(g => g.MaUser == currentEmployerId
                                && g.TrangThai == true
                                && g.MaGoiNavigation != null
                                && (
                                    g.MaGoi == 3 || g.MaGoi == 4 ||
                                    (g.MaGoiNavigation.LoaiGoi == 2 && g.MaGoiNavigation.DonViThoiGian == 6) ||
                                    (g.MaGoiNavigation.LoaiGoi == 3 && g.MaGoiNavigation.DonViThoiGian == 1)
                                ));
            }

            return Ok(new { isPremium = hasAiFeature });
        }

        // ===================================================================
        // API 2: LẤY DANH SÁCH ỨNG VIÊN (TRẢ VỀ MẢNG THUẦN CandidateDto[])
        // ===================================================================
        [HttpGet("jobs/{maViTri}/candidates")]
        public async Task<IActionResult> GetCandidates(int maViTri)
        {
            var candidates = await _context.DonUngTuyens
                .Include(d => d.ChiTietPhanTichAi)
                .Include(d => d.MaCvNavigation)
                    .ThenInclude(cv => cv.MaUserNavigation)
                .Where(d => d.MaViTri == maViTri)
                .Select(d => new CandidateDto
                {
                    MaDon = d.MaDon,
                    HoTen = d.MaCvNavigation.MaUserNavigation.HoTen,
                    Email = d.MaCvNavigation.MaUserNavigation.Email,
                    CvUrl = d.MaCvNavigation.DuongDan,
                    ThuGioiThieu = d.ThuGioiThieu,
                    NgayNop = d.NgayNop,
                    TrangThai = d.TrangThai,
                    GhiChu = d.GhiChu,
                    DiemMatchingTong = d.ChiTietPhanTichAi != null ? d.ChiTietPhanTichAi.DiemMatchingTong : 0,
                    ProfileAiJson = d.ChiTietPhanTichAi != null ? d.ChiTietPhanTichAi.ThongTinHoSoTrichXuatJson : null,

                    // CẬP NHẬT ĐIỀU KIỆN: Kiểm tra thêm điểm 0 hoặc JSON rỗng "{}"
                    IsPendingAi = d.ChiTietPhanTichAi == null
                               || d.ChiTietPhanTichAi.DiemMatchingTong == 0
                               || d.ChiTietPhanTichAi.ThongTinHoSoTrichXuatJson == "{}"
                })
                .OrderByDescending(d => d.DiemMatchingTong)
                .ThenByDescending(d => d.NgayNop)
                .ToListAsync();

            return Ok(candidates);
        }

        [HttpPut("applications/{maDon}/status")]
        public async Task<IActionResult> UpdateApplicationStatus(int maDon, [FromBody] UpdateStatusDto request)
        {
            var donUngTuyen = await _context.DonUngTuyens
                .Include(d => d.MaCvNavigation)
                    .ThenInclude(cv => cv.MaUserNavigation)
                .Include(d => d.MaViTriNavigation)
                    .ThenInclude(v => v.MaTinNavigation)
                .FirstOrDefaultAsync(d => d.MaDon == maDon);

            if (donUngTuyen == null) return NotFound("Không tìm thấy đơn ứng tuyển.");

            if (request.Status < donUngTuyen.TrangThai && !(donUngTuyen.TrangThai == 3 && request.Status == 2))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Không thể chuyển ngược trạng thái đơn ứng tuyển về các bước trước đó trong phễu!"
                });
            }

            donUngTuyen.TrangThai = (byte)request.Status;
            if (request.GhiChu != null)
            {
                donUngTuyen.GhiChu = request.GhiChu;
            }

            if (request.Status == 2)
            {
                var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaCongTy == donUngTuyen.MaViTriNavigation.MaTinNavigation.MaCongTy);
                string emailUngVien = donUngTuyen.MaCvNavigation?.MaUserNavigation?.Email;
                string tenUngVien = donUngTuyen.MaCvNavigation?.MaUserNavigation?.HoTen ?? "Ứng viên";
                string tenViTri = donUngTuyen.MaViTriNavigation?.TenViTri ?? "Vị trí đã ứng tuyển";

                if (!string.IsNullOrEmpty(emailUngVien) && company != null)
                {
                    string chuDe = $"[{company.TenCongTy}] Thư mời tham gia phỏng vấn - Vị trí {tenViTri}";

                    // 1. CỐ ĐỊNH: Khung layout Branding cao cấp (Master Wrapper Layout) luôn luôn sử dụng để bọc ngoài thư
                    string masterLayout = @"<div style='max-width: 620px; margin: 20px auto; font-family: ""Segoe UI"", Arial, sans-serif; color: #333333; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.06);'>
                        <div style='background-color: #1e3a8a; padding: 26px; text-align: center;'>
                            <h2 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: 600; letter-spacing: 0.5px;'>THƯ MỜI PHỎNG VẤN</h2>
                        </div>
                        <div style='padding: 32px 24px; background-color: #ffffff; font-size: 15px; line-height: 1.7; color: #334155;'>
                            {NoiDungThuCuaDoanhNghiep}
                        </div>
                        <div style='background-color: #f8fafc; padding: 20px; text-align: center; font-size: 13px; color: #64748b; border-top: 1px solid #e2e8f0;'>
                            Hệ thống mạng lưới việc làm cao cấp <strong style='color: #1e3a8a;'>JobsNow System</strong>
                        </div>
                    </div>";

                    // 2. Đọc nội dung thư từ NTD soạn, nếu trống thì dùng văn bản mẫu mặc định của hệ thống
                    string thongDiepGoc = !string.IsNullOrEmpty(company.MauEmailInterview)
                        ? company.MauEmailInterview
                        : "Chào {TenUngVien},\n\nCông ty {TenCongTy} trân trọng mời bạn tham gia phỏng vấn vị trí {TenViTri}.\n• Thời gian: {ThoiGian}\n• Địa điểm: {DiaDiem}\n\n{LinkBaiTest}\n\nTrân trọng,\n{ChuKyEmail}";

                    string thongDiepHtml = thongDiepGoc.Replace("\n", "<br/>");

                    string testLinkHtml = "";
                    if (!string.IsNullOrEmpty(request.LinkBaiTest))
                    {
                        testLinkHtml = $"<a href='{request.LinkBaiTest}' target='_blank' style='background-color: #10b981; color: #ffffff; padding: 6px 14px; text-decoration: none; display: inline-block; font-size: 13px; font-weight: bold; border-radius: 4px; margin: 0 4px; box-shadow: 0 2px 4px rgba(16,185,129,0.15);'>🚀 BẮT ĐẦU LÀM BÀI TEST</a>";
                    }

                    // 4. Chuẩn hóa khối chữ ký doanh nghiệp
                    string chuKyHtml = !string.IsNullOrEmpty(company.ChuKyEmail)
                        ? $"<div style='margin-top: 20px; padding-top: 12px; border-top: 1px dashed #cbd5e1; color: #475569; font-size: 13px;'>{company.ChuKyEmail.Replace("\n", "<br/>")}</div>"
                        : "";

                    // 5. Tiến hành quét trộn dữ liệu động vào nội dung thư
                    string bodyText = thongDiepHtml
                        .Replace("{TenUngVien}", tenUngVien)
                        .Replace("{TenViTri}", tenViTri)
                        .Replace("{ThoiGian}", request.ThoiGian ?? "Sẽ thông báo sau")
                        .Replace("{DiaDiem}", request.DiaDiem ?? "Sẽ thông báo sau")
                        .Replace("{TenCongTy}", company.TenCongTy);

                    // 6. Cơ chế phòng vệ vị trí đặt từ khóa của Nhà tuyển dụng
                    // Nếu trong văn bản có ghi sẵn từ khóa {LinkBaiTest} -> Đổ nút bấm vào đúng chỗ đó
                    if (bodyText.Contains("{LinkBaiTest}"))
                    {
                        bodyText = bodyText.Replace("{LinkBaiTest}", testLinkHtml);
                    }
                    else if (!string.IsNullOrEmpty(testLinkHtml))
                    {
                        bodyText += "<br/><br/>" + testLinkHtml;
                    }

                    if (bodyText.Contains("{ChuKyEmail}"))
                    {
                        bodyText = bodyText.Replace("{ChuKyEmail}", chuKyHtml);
                    }
                    else
                    {
                        bodyText += chuKyHtml;
                    }

                    string noiDungGuiDi = masterLayout.Replace("{NoiDungThuCuaDoanhNghiep}", bodyText);

                    await _emailService.SendEmailAsync(emailUngVien, chuDe, noiDungHtml: noiDungGuiDi);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Cập nhật trạng thái thành công" });
        }

        [HttpGet("my-jobs")]
        public async Task<IActionResult> GetMyJobs()
        {
            int maUser = GetCurrentUserId();
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);

            if (company == null)
            {
                return Ok(new { status = "NO_PROFILE", message = "Cần khởi tạo hồ sơ công ty trước khi quản lý tin đăng." });
            }

            if (company.TrangThai == false)
            {
                return Ok(new { status = "PENDING_APPROVAL", message = "Hồ sơ doanh nghiệp đang chờ duyệt. Vui lòng quay lại sau." });
            }

            // A. TỰ ĐỘNG GỠ TIN HẾT HẠN
            var expiredJobs = await _context.TinTuyenDungs
                .Where(t => t.MaCongTy == company.MaCongTy && t.TrangThai == 1 && t.NgayHetHan < DateTime.Now)
                .ToListAsync();

            if (expiredJobs.Any())
            {
                foreach (var job in expiredJobs)
                {
                    job.TrangThai = 2;
                }
                await _context.SaveChangesAsync();
            }

            // B. NẠP DỮ LIỆU TỪ SQL VỀ BỘ NHỚ (Dùng OrderByDescending theo MaTin nếu chưa chạy Migration NgayDang)
            var rawJobs = await _context.TinTuyenDungs
                .Include(t => t.ChiTietViTris)
                    .ThenInclude(v => v.MaNganhNavigation)
                .Include(t => t.ChiTietViTris)
                    .ThenInclude(v => v.MaPhuongNavigation)
                        .ThenInclude(p => p.MaTpNavigation)
                .Include(t => t.ChiTietViTris)
                    .ThenInclude(v => v.DonUngTuyens)
                .Where(t => t.MaCongTy == company.MaCongTy)
                .OrderByDescending(t => t.MaTin)
                .ToListAsync();

            // C. XỬ LÝ NỐI CHUỖI VÀ TẠO MẢNG TRÊN RAM (AN TOÀN KHÔNG BỊ LỖI SQL)
            var myJobs = rawJobs.Select(t => new
            {
                maTin = t.MaTin,
                maViTri = t.ChiTietViTris.Select(v => (int?)v.MaViTri).FirstOrDefault() ?? t.MaTin,
                tieuDe = t.TieuDeChienDich,
                ngayTao = t.NgayDang, // Hoặc t.NgayHetHan nếu chưa migration
                hanNop = t.NgayHetHan,
                trangThai = t.TrangThai,
                isPromoted = t.IsPromoted,
                soLuongUngVien = t.ChiTietViTris.SelectMany(v => v.DonUngTuyens ?? new List<DonUngTuyen>()).Count(),

                // Ngành nghề
                danhSachMaNganh = t.ChiTietViTris.Select(v => v.MaNganh).Distinct().ToList(),
                danhSachNganhObj = t.ChiTietViTris
                    .Where(v => v.MaNganhNavigation != null)
                    .Select(v => new { id = v.MaNganh, name = v.MaNganhNavigation.TenNganh })
                    .GroupBy(x => x.id)
                    .Select(g => g.First())
                    .ToList(),
                tenNganhNghe = string.Join(", ", t.ChiTietViTris
                    .Where(v => v.MaNganhNavigation != null)
                    .Select(v => v.MaNganhNavigation.TenNganh)
                    .Distinct()),

                // Khu vực
                danhSachKhuVuc = t.ChiTietViTris
                    .Where(v => v.MaPhuongNavigation != null)
                    .Select(v => v.MaPhuongNavigation.TenPhuong + (v.MaPhuongNavigation.MaTpNavigation != null ? ", " + v.MaPhuongNavigation.MaTpNavigation.TenTp : ""))
                    .Distinct().ToList()
            }).ToList();

            return Ok(new { status = "SUCCESS", data = myJobs });
        }


        // ===================================================================
        // API 1: LẤY SỐ LƯỢT XEM CV CÒN LẠI CỦA DOANH NGHIỆP (ĐỘC LẬP)
        // ===================================================================
        [HttpGet("cv-credits")]
        public async Task<IActionResult> GetCvCredits()
        {
            try
            {
                int currentEmployerId = GetCurrentUserId();
                var user = await _context.Users.FindAsync(currentEmployerId);

                return Ok(new
                {
                    success = true,
                    luotXemCvConLai = user?.LuotXemCvConLai ?? 0
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, luotXemCvConLai = 0, error = ex.Message });
            }
        }

        // ===================================================================
        // API 2: LẤY DANH SÁCH HỒ SƠ CV (HỖ TRỢ TÌM KIẾM/LỌC)
        // ===================================================================
        [HttpGet("hunt-cv")]
        public async Task<IActionResult> HuntCv(
            [FromQuery] string? keyword,
            [FromQuery] string? nganhNghe,
            [FromQuery] string? nganhNgheKhac,
            [FromQuery] string? skills)
        {
            try
            {
                int currentEmployerId = GetCurrentUserId();

                // 1. Lấy thông tin Công ty để loại trừ các CV đã từng nộp vào Công ty này
                var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == currentEmployerId);
                List<int> appliedCvIds = new List<int>();

                if (company != null)
                {
                    appliedCvIds = await _context.DonUngTuyens
                        .Where(d => d.MaViTriNavigation.MaTinNavigation.MaCongTy == company.MaCongTy)
                        .Select(d => d.MaCv)
                        .Distinct()
                        .ToListAsync();
                }

                // 2. Query cơ bản: Chỉ lấy CV Bật IsPublic và Chưa từng ứng tuyển vào Công ty
                var query = _context.Cvs
                    .Include(c => c.MaUserNavigation)
                    .Include(c => c.NganhNghe) // ✨ Nạp thêm bảng Ngành nghề
                    .Where(c => (c.IsPublic == true) && !appliedCvIds.Contains(c.MaCv))
                    .AsQueryable();

                // 3. LỌC THEO TỪ KHÓA CHÍNH (Tên ứng viên / Tiêu đề / Nội dung CV)
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string kw = keyword.Trim().ToLower();
                    query = query.Where(c =>
                        (c.TieuDe != null && c.TieuDe.ToLower().Contains(kw)) ||
                        (c.MaUserNavigation != null && c.MaUserNavigation.HoTen.ToLower().Contains(kw)) ||
                        (c.DuLieuCv != null && c.DuLieuCv.ToLower().Contains(kw))
                    );
                }

                // 4. LỌC THEO NGÀNH NGHỀ (Xử lý thông minh khi chọn "Khác")
                string? targetIndustry = (nganhNghe == "Khác" && !string.IsNullOrWhiteSpace(nganhNgheKhac))
                    ? nganhNgheKhac
                    : nganhNghe;

                if (!string.IsNullOrWhiteSpace(targetIndustry) && targetIndustry != "Khác")
                {
                    string ind = targetIndustry.Trim().ToLower();
                    query = query.Where(c =>
                        (c.NganhNghe != null && c.NganhNghe.TenNganh.ToLower().Contains(ind)) ||
                        (c.TieuDe != null && c.TieuDe.ToLower().Contains(ind))
                    );
                }

                // 5. LỌC THEO KỸ NĂNG / CÔNG NGHỆ (Tìm trong Tiêu đề và Nội dung CV)
                if (!string.IsNullOrWhiteSpace(skills))
                {
                    string sk = skills.Trim().ToLower();
                    query = query.Where(c =>
                        (c.TieuDe != null && c.TieuDe.ToLower().Contains(sk)) ||
                        (c.DuLieuCv != null && c.DuLieuCv.ToLower().Contains(sk))
                    );
                }

                // 6. Sắp xếp kết quả mới nhất
                var rawList = await query
                    .OrderByDescending(c => c.NgayCapNhat)
                    .ThenByDescending(c => c.MaCv)
                    .ToListAsync();

                // 7. Lấy danh sách ID các CV mà Nhà tuyển dụng này ĐÃ MỞ KHÓA
                var unlockedCvIds = await _context.LichSuMoKhoaCvs
                    .Where(l => l.MaUser == currentEmployerId)
                    .Select(l => l.MaCv)
                    .ToListAsync();

                var result = rawList.Select(c => {
                    bool isUnlocked = unlockedCvIds.Contains(c.MaCv);

                    string? extractedJobTitle = null;
                    if (!string.IsNullOrWhiteSpace(c.DuLieuCv))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(c.DuLieuCv);
                            if (doc.RootElement.TryGetProperty("personalInfo", out var personalInfoElement) &&
                                personalInfoElement.TryGetProperty("jobTitle", out var jobTitleElement))
                            {
                                extractedJobTitle = jobTitleElement.GetString();
                            }
                        }
                        catch
                        {
                            // Bỏ qua lỗi nếu JSON hỏng/không đúng định dạng
                        }
                    }

                    return new
                    {
                        maCv = c.MaCv,
                        maUser = c.MaUser,
                        hoTen = c.MaUserNavigation?.HoTen ?? "Ứng viên ẩn danh",
                        // Ưu tiên lấy jobTitle từ JSON -> TieuDe -> Mặc định
                        jobTitle = !string.IsNullOrWhiteSpace(extractedJobTitle)
                        ? extractedJobTitle
                        : (!string.IsNullOrWhiteSpace(c.TieuDe) ? c.TieuDe : "Chưa cập nhật vị trí"),
                        email = isUnlocked ? c.MaUserNavigation?.Email : "••••••••@gmail.com",
                        tieuDe = c.TieuDe ?? "Hồ sơ ứng viên",
                        tenNganh = c.NganhNghe?.TenNganh ?? "Chưa phân loại",
                        cvUrl = c.DuongDan,
                        isUnlocked = isUnlocked,
                        ngayCapNhat = c.NgayCapNhat?.ToString("dd/MM/yyyy") ?? "Mới cập nhật"
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                string detailError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tìm kiếm CV!", error = detailError });
            }
        }

        // API mo khoa thong tin lien he cua ung vien
        [HttpPost("unlock-cv/{maCv}")]
        public async Task<IActionResult> UnlockCv(int maCv)
        {
            int currentEmployerId = GetCurrentUserId();
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(currentEmployerId);
                if (user == null) return Unauthorized(new { success = false, message = "Phiên đăng nhập hết hạn." });

                // Kiem tra thoi han goi va so luong luot xem con lai cua doanh nghiep
                bool isUserValid = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value >= DateTime.Now;
                if (!isUserValid || user.LuotXemCvConLai <= 0)
                {
                    return BadRequest("Gói dịch vụ đã hết hạn sử dụng hoặc tài khoản đã hết lượt mở khóa hồ sơ!");
                }

                user.LuotXemCvConLai -= 1;

                _context.LichSuMoKhoaCvs.Add(new LichSuMoKhoaCV
                {
                    MaUser = currentEmployerId,
                    MaCv = maCv,
                    NgayMoKhoa = DateTime.Now
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Mở khóa thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi mở khóa.", error = ex.Message });
            }
        }

        [HttpGet("hunt-cv/industries")]
        public async Task<IActionResult> GetDatabaseIndustries()
        {
            var industries = await _context.NganhNghes
                .OrderBy(n => n.MaNganh)
                .Select(n => n.TenNganh)
                .ToListAsync();

            // Cơ chế phòng vệ: Đảm bảo luôn có tùy chọn "Khác" ở cuối danh sách 
            // để kích hoạt ô nhập liệu thông minh ở giao diện Frontend
            if (!industries.Contains("Khác"))
            {
                industries.Add("Khác");
            }

            return Ok(industries);
        }

        // ===================================================================
        // HÀM HỖ TRỢ DÙNG CHUNG
        // ===================================================================

        private int GetCurrentUserId()
        {
            var claim = User.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier ||
                c.Type == "nameid" ||
                c.Type == "sub" ||
                c.Type == "maUser" ||
                c.Type == "userId" ||
                c.Type.EndsWith("nameidentifier")
            );

            if (claim != null && int.TryParse(claim.Value, out int userId))
            {
                return userId;
            }

            return 0; // Trả về 0 thay vì quăng Exception để Controller xử lý HTTP 401 chuẩn
        }
    }
}