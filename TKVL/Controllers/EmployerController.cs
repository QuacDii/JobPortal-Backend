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
                duLieuChoDuyetJson = company.DuLieuChoDuyetJson, // Trả về trường bản nháp để Frontend nhận diện trạng thái chờ duyệt
            });
        }

        [HttpPost("company")]
        public async Task<IActionResult> UpdateCompanyProfile([FromForm] CompanyProfileDto dto)
        {
            int maUser = GetCurrentUserId();
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
            bool isNew = (company == null);

            // ---------------------------------------------------------------
            // 1. VALIDATION DỮ LIỆU ĐẦU VÀO
            // ---------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(dto.TenCongTy))
                return BadRequest(new { success = false, message = "Vui lòng nhập tên công ty!" });

            string targetMst = dto.MaSoThue?.Trim();
            if (string.IsNullOrWhiteSpace(targetMst))
                return BadRequest(new { success = false, message = "Vui lòng nhập mã số thuế!" });

            // Kiểm tra Mã số thuế chỉ được nhập chữ số
            if (!Regex.IsMatch(targetMst, @"^\d+$"))
                return BadRequest(new { success = false, message = "Mã số thuế chỉ được nhập chữ số!" });

            // Kiểm tra Quy mô nhân sự chỉ được nhập chữ số
            if (!string.IsNullOrWhiteSpace(dto.QuyMo) && !Regex.IsMatch(dto.QuyMo.Trim(), @"^\d+$"))
                return BadRequest(new { success = false, message = "Quy mô nhân sự chỉ được nhập chữ số!" });

            // Kiểm tra Bắt buộc có Giới thiệu công ty
            if (string.IsNullOrWhiteSpace(dto.MoTa))
                return BadRequest(new { success = false, message = "Vui lòng nhập giới thiệu về công ty!" });

            // ---------------------------------------------------------------
            // 2. KIỂM TRA TRÙNG MÃ SỐ THUẾ VỚI DOANH NGHIỆP KHÁC
            // ---------------------------------------------------------------
            bool isMstExisted = await _context.CongTies.AnyAsync(c => c.MaSoThue == targetMst && c.MaUser != maUser);
            if (isMstExisted)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Mã số thuế '{targetMst}' đã được đăng ký bởi một doanh nghiệp khác trên hệ thống!"
                });
            }

            // ---------------------------------------------------------------
            // 3. UPLOAD FILE MỚI VÀ VALIDATION FILE BẮT BUỘC
            // ---------------------------------------------------------------
            string newLogoUrl = dto.LogoFile != null ? await _cloudinaryService.UploadLogoAsync(dto.LogoFile) : null;
            string newFrontUrl = dto.GiayPhepKinhDoanhMatTruocFile != null ? await _cloudinaryService.UploadGpkdAsync(dto.GiayPhepKinhDoanhMatTruocFile) : null;
            string newBackUrl = dto.GiayPhepKinhDoanhMatSauFile != null ? await _cloudinaryService.UploadGpkdAsync(dto.GiayPhepKinhDoanhMatSauFile) : null;

            // Bắt buộc có Logo (Logo mới upload HOẶC Logo cũ đã có)
            string finalLogo = newLogoUrl ?? company?.Logo;
            if (string.IsNullOrEmpty(finalLogo))
            {
                return BadRequest(new { success = false, message = "Vui lòng tải lên Logo của doanh nghiệp!" });
            }

            // Bắt buộc có Giấy phép kinh doanh Mặt trước (Mới upload HOẶC cũ đã có)
            string finalFront = newFrontUrl ?? company?.GiayPhepKinhDoanhMatTruoc;
            if (string.IsNullOrEmpty(finalFront))
            {
                return BadRequest(new { success = false, message = "Vui lòng tải lên Giấy phép kinh doanh (Mặt trước / Bản chính)!" });
            }

            string targetBack = newBackUrl ?? company?.GiayPhepKinhDoanhMatSau;

            if (isNew)
            {
                company = new CongTy { MaUser = maUser };
            }

            // Cập nhật các thông tin phụ (Luôn có hiệu lực ngay)
            company.Logo = finalLogo;
            company.QuyMo = dto.QuyMo?.Trim();
            company.DiaChi = dto.DiaChi;
            company.MoTa = dto.MoTa;
            company.MauEmailInterview = dto.MauEmailInterview;

            // Kiểm tra xem có sự thay đổi thông tin pháp lý hay không
            bool hasLegalChanges = isNew ||
                company.TenCongTy != dto.TenCongTy ||
                company.MaSoThue != targetMst ||
                newFrontUrl != null ||
                newBackUrl != null;

            if (isNew || company.TrangThai == false)
            {
                // KỊCH BẢN A: Hồ sơ mới hoặc chưa duyệt -> Cập nhật trực tiếp & chờ duyệt
                company.TenCongTy = dto.TenCongTy;
                company.MaSoThue = targetMst;
                company.GiayPhepKinhDoanhMatTruoc = finalFront;
                company.GiayPhepKinhDoanhMatSau = targetBack;
                company.TrangThai = false; // Chờ Admin duyệt lần đầu
                company.DuLieuChoDuyetJson = null;

                if (isNew) _context.CongTies.Add(company);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, isPending = true, message = "Lưu hồ sơ thành công! Yêu cầu đang chờ Ban quản trị phê duyệt." });
            }
            else if (hasLegalChanges)
            {
                // KỊCH BẢN B: Hồ sơ ĐÃ ĐƯỢC DUYỆT (TrangThai == true) nhưng NTD sửa thông tin pháp lý
                // -> Không khóa tài khoản! Lưu thông tin mới vào bản nháp (Draft) chờ duyệt
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
                    message = "Thông tin giới thiệu/logo đã được cập nhật ngay! Yêu cầu thay đổi thông tin pháp lý đã gửi Ban quản trị thẩm định (tài khoản vẫn hoạt động bình thường)."
                });
            }

            // KỊCH BẢN C: Chỉ sửa thông tin phụ trên hồ sơ đã duyệt -> Hoàn tất ngay
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
                    DiemMatchingTong = d.ChiTietPhanTichAi.DiemMatchingTong, // Nếu tên thuộc tính trong DB của bạn là DiemMatching thì đổi lại tương ứng
                    ProfileAiJson = d.ChiTietPhanTichAi.ThongTinHoSoTrichXuatJson
                })
                .OrderByDescending(d => d.NgayNop)
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

            var myJobs = await _context.ChiTietViTris
                .Include(v => v.MaTinNavigation)
                .Where(v => v.MaTinNavigation.MaCongTy == company.MaCongTy)
                .Select(v => new
                {
                    maViTri = v.MaViTri,
                    tieuDe = v.MaTinNavigation.TieuDeChienDich + " - " + v.TenViTri,
                    ngayTao = v.MaTinNavigation.NgayHetHan,
                    trangThai = v.MaTinNavigation.TrangThai,
                    soLuongUngVien = _context.DonUngTuyens.Count(d => d.MaViTri == v.MaViTri)
                })
                .OrderByDescending(v => v.ngayTao)
                .ToListAsync();

            return Ok(new { status = "SUCCESS", data = myJobs });
        }

        // API săn tìm ứng viên tích hợp bộ lọc nâng cao (Ngành nghề, Kỹ năng, Từ khóa)
        // API săn tìm ứng viên công khai tích hợp bộ lọc ngành nghề cố định và kỹ năng nâng cao
        [HttpGet("hunt-cv")]
        public async Task<IActionResult> HuntCv(string keyword = "", string nganhNghe = "", string skills = "", string nganhNgheKhac = "")
        {
            int currentEmployerId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(currentEmployerId);
            if (user == null) return Unauthorized(new { success = false, message = "Phiên đăng nhập hết hạn." });
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == currentEmployerId);
            if (company == null) return BadRequest(new { success = false, message = "Tài khoản doanh nghiệp chưa khởi tạo hồ sơ công ty." });

            if (company.TrangThai == false)
                return BadRequest(new { success = false, message = "Hồ sơ công ty của bạn đang trong quá trình chờ Admin phê duyệt!" });

            if (!user.NgayHetHanGoi.HasValue || user.NgayHetHanGoi.Value < DateTime.Now)
            {
                return Ok(new { success = false, isPremium = false, luotXemCvConLai = 0, data = new List<HuntCvDto>(), message = "Gói dịch vụ tìm ứng viên đã hết hạn." });
            }

            var query = _context.Cvs.Include(c => c.MaUserNavigation).Where(c => c.IsPublic == true);

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(c => c.TieuDe.Contains(keyword) || c.MaUserNavigation.HoTen.Contains(keyword));

            if (!string.IsNullOrEmpty(skills))
                query = query.Where(c => c.DuLieuCv.Contains(skills));

            if (!string.IsNullOrEmpty(nganhNghe))
            {
                if (nganhNghe == "Khác" && !string.IsNullOrEmpty(nganhNgheKhac))
                {
                    query = query.Where(c => c.DuLieuCv.Contains(nganhNgheKhac));
                }
                else if (nganhNghe != "Khác")
                {
                    query = query.Where(c => c.DuLieuCv.Contains(nganhNghe));
                }
            }

            query = query.Where(c => !_context.DonUngTuyens
                .Any(d => d.MaCv == c.MaCv && d.MaViTriNavigation.MaTinNavigation.MaCongTy == company.MaCongTy));

            var cvList = await query.ToListAsync();

            var unlockedCvIds = await _context.LichSuMoKhoaCvs
                .Where(l => l.MaUser == currentEmployerId) 
                .Select(l => l.MaCv) 
                .ToListAsync();  

            var results = cvList.Select(c => new HuntCvDto
            {
                MaCv = c.MaCv,               
                HoTen = c.MaUserNavigation.HoTen,                
                IsUnlocked = unlockedCvIds.Contains(c.MaCv),
                Email = unlockedCvIds.Contains(c.MaCv) ? c.MaUserNavigation.Email : "hoang***@gmail.com",
                CvUrl = c.DuongDan
            }).ToList();

            return Ok(new { success = true, isPremium = true, luotXemCvConLai = user.LuotXemCvConLai, data = results });

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
            var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                     ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                     ?? User.Claims.FirstOrDefault(c => c.Type == "sub");
            return int.Parse(claim.Value);
        }
    }
}