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

            if (!string.IsNullOrWhiteSpace(dto.QuyMo) && !Regex.IsMatch(dto.QuyMo.Trim(), @"^[\p{L}\p{N}\s]+$"))
                return BadRequest(new { success = false, message = "Quy mô nhân sự chỉ được nhập chữ và số, không chứa ký tự đặc biệt!" });

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

            // 🌟 3. UPLOAD FILE MỚI VÀ BẢO VỆ BẮT LỖI CLOUDINARY
            string? newLogoUrl = null;
            if (dto.LogoFile != null && dto.LogoFile.Length > 0)
            {
                try
                {
                    newLogoUrl = await _cloudinaryService.UploadLogoAsync(dto.LogoFile);
                    if (string.IsNullOrEmpty(newLogoUrl))
                    {
                        return BadRequest(new { success = false, message = "Lỗi: Cloudinary trả về URL logo rỗng. Kiểm tra lại cấu hình Cloudinary!" });
                    }
                }
                catch (Exception ex)
                {
                    return BadRequest(new { success = false, message = $"Lỗi khi tải Logo lên Cloudinary: {ex.Message}" });
                }
            }

            string? newFrontUrl = null;
            if (dto.GiayPhepKinhDoanhMatTruocFile != null && dto.GiayPhepKinhDoanhMatTruocFile.Length > 0)
            {
                try
                {
                    newFrontUrl = await _cloudinaryService.UploadGpkdAsync(dto.GiayPhepKinhDoanhMatTruocFile);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { success = false, message = $"Lỗi khi tải GPKD mặt trước lên Cloudinary: {ex.Message}" });
                }
            }

            string? newBackUrl = null;
            if (dto.GiayPhepKinhDoanhMatSauFile != null && dto.GiayPhepKinhDoanhMatSauFile.Length > 0)
            {
                try
                {
                    newBackUrl = await _cloudinaryService.UploadGpkdAsync(dto.GiayPhepKinhDoanhMatSauFile);
                }
                catch (Exception ex)
                {
                    return BadRequest(new { success = false, message = $"Lỗi khi tải GPKD mặt sau lên Cloudinary: {ex.Message}" });
                }
            }

            // 🌟 4. KIỂM TRA LOGO & GPKD CUỐI CÙNG
            string? finalLogo = newLogoUrl ?? company?.Logo;
            if (string.IsNullOrEmpty(finalLogo))
            {
                return BadRequest(new
                {
                    success = false,
                    message = dto.LogoFile == null
                        ? "Backend không nhận được tệp LogoFile từ Client (dto.LogoFile = null)!"
                        : "Vui lòng tải lên Logo của doanh nghiệp!"
                });
            }

            string? finalFront = newFrontUrl ?? company?.GiayPhepKinhDoanhMatTruoc;
            if (string.IsNullOrEmpty(finalFront))
            {
                return BadRequest(new { success = false, message = "Vui lòng tải lên Giấy phép kinh doanh (Mặt trước / Bản chính)!" });
            }

            string? targetBack = newBackUrl ?? company?.GiayPhepKinhDoanhMatSau;

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
            company.ChuKyEmail = dto.ChuKyEmail;

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

            // 🌟 KIỂM TRA TRỰC TIẾP TRONG BẢNG User_DacQuyen VỚI MA_CODE "NTD_AI_MATCHING"
            bool hasAiFeature = await _context.UserDacQuyens
                .Include(ud => ud.DacQuyen)
                .AnyAsync(ud => ud.MaUser == currentEmployerId
                             && ud.NgayHetHan.Date >= DateTime.Now.Date
                             && ud.DacQuyen != null
                             && ud.DacQuyen.MaCode == "NTD_AI_MATCHING");

            return Ok(new { isPremium = hasAiFeature });
        }

        // ===================================================================
        // API 2: LẤY DANH SÁCH ỨNG VIÊN (TRẢ VỀ MẢNG THUẦN CandidateDto[])
        // ===================================================================
        [HttpGet("jobs/{maViTri}/candidates")]
        [Authorize(Roles = "1")] // Chỉ dành cho Nhà tuyển dụng
        public async Task<IActionResult> GetCandidatesByJob(int maViTri)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (userIdClaim == null) return Unauthorized(new { message = "Vui lòng đăng nhập!" });
            int maUser = int.Parse(userIdClaim.Value);

            // 1. Kiểm tra vị trí tuyển dụng có thuộc về công ty của NTD này không
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
            if (company == null)
                return BadRequest(new { message = "Không tìm thấy thông tin công ty." });

            var jobExists = await _context.TinTuyenDungs
                .AnyAsync(b => b.ChiTietViTris.Select(c => c.MaViTri).Contains(maViTri) && b.MaCongTy == company.MaCongTy);

            if (!jobExists)
                return NotFound(new { message = "Không tìm thấy bài tuyển dụng hoặc bạn không có quyền truy cập." });

            // 2. Query danh sách đơn ứng tuyển & Include ChiTietPhanTichAi
            var candidates = await _context.DonUngTuyens
                .Include(d => d.MaCvNavigation)
                    .ThenInclude(u => u.MaUserNavigation)
                .Include(d => d.ChiTietPhanTichAi) // Join bảng kết quả AI
                .Where(d => d.MaViTri == maViTri)
                .OrderByDescending(d => d.NgayNop)
                .Select(d => new CandidateFunnelDto
                {
                    MaDon = d.MaDon,
                    MaUngVien = d.MaCvNavigation.MaUser,
                    HoTen = d.MaCvNavigation.MaUserNavigation.HoTen,
                    Email = d.MaCvNavigation.MaUserNavigation.Email,
                    NgayNop = d.NgayNop,
                    TrangThai = d.TrangThai,
                    CvUrl = d.MaCvNavigation.DuLieuCv,

                    // Kiểm tra xem đơn này đã được AI chấm điểm chưa
                    IsPendingAi = d.ChiTietPhanTichAi == null,

                    // 🌟 ÉP KIỂU (double) ĐỂ FIX LỖI Math.Round (CS0121)
                    DiemMatchingTong = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemMatchingTong) : 0,
                    DiemKyNang = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemKyNang) : 0,
                    DiemKinhNghiem = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemKinhNghiem) : 0,
                    DiemLinhVuc = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemLinhVuc) : 0,
                    DiemCapBac = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemCapBac) : 0,

                    DiemManhTieuBieu = d.ChiTietPhanTichAi != null ? d.ChiTietPhanTichAi.DiemManhTieuBieu : null,
                    DiemConThieu = d.ChiTietPhanTichAi != null ? d.ChiTietPhanTichAi.DiemConThieu : null
                })
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
                        testLinkHtml = $"<a href='{request.LinkBaiTest}' target='_blank' style='background-color: #10b981; color: #ffffff; padding: 6px 14px; text-decoration: none; display: inline-block; font-size: 13px; font-weight: bold; border-radius: 4px; margin: 0 4px; box-shadow: 0 2px 4px rgba(16,185,129,0.15);'>BẮT ĐẦU LÀM BÀI TEST</a>";
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

        [HttpPatch("jobs/{maTin}/toggle-status")]
        public async Task<IActionResult> ToggleJobStatus(int maTin)
        {
            int currentUserId = GetCurrentUserId();
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == currentUserId);
            if (company == null) return BadRequest(new { message = "Không tìm thấy thông tin doanh nghiệp!" });

            var job = await _context.TinTuyenDungs
                .FirstOrDefaultAsync(j => j.MaTin == maTin && j.MaCongTy == company.MaCongTy);

            if (job == null) return NotFound(new { message = "Tin tuyển dụng không tồn tại hoặc không thuộc quyền sở hữu!" });

            // Đổi trạng thái: Đang đăng (1) <-> Tạm dừng/Ẩn (2)
            if (job.TrangThai == 1)
            {
                job.TrangThai = 2; // Tạm dừng
            }
            else if (job.TrangThai == 2)
            {
                // Kiểm tra xem đã hết hạn chưa trước khi mở lại
                if (job.NgayHetHan < DateTime.Now)
                {
                    return BadRequest(new { success = false, message = "Tin tuyển dụng đã quá hạn! Vui lòng gia hạn ngày trước khi bật lại." });
                }
                job.TrangThai = 1; // Bật lại
            }
            else
            {
                return BadRequest(new { success = false, message = "Chỉ có thể ẩn/hiện tin đang đăng hoặc tạm dừng!" });
            }

            await _context.SaveChangesAsync();

            string statusName = job.TrangThai == 1 ? "Đang đăng" : "Tạm dừng/Ẩn";
            return Ok(new { success = true, newStatus = job.TrangThai, message = $"Đã chuyển trạng thái tin sang '{statusName}'!" });
        }

        [HttpGet("dashboard/analytics")]
        public async Task<IActionResult> GetDashboardAnalytics(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int days = 30)
        {
            int currentUserId = GetCurrentUserId();
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == currentUserId);
            if (company == null) return BadRequest(new { message = "Không tìm thấy doanh nghiệp!" });

            int maCongTy = company.MaCongTy;

            // 1. XÁC ĐỊNH KHOẢNG THỜI GIAN
            DateTime end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Now;
            DateTime start = startDate?.Date ?? DateTime.Now.Date.AddDays(-days + 1);
            int totalDays = (end - start).Days + 1;

            var allCompanyJobs = await _context.TinTuyenDungs
                .Include(j => j.ChiTietViTris)
                .Where(j => j.MaCongTy == maCongTy)
                .ToListAsync();

            var maTinList = allCompanyJobs.Select(j => j.MaTin).ToList();
            var maViTriList = allCompanyJobs.SelectMany(j => j.ChiTietViTris).Select(v => v.MaViTri).ToList();

            // 2. TÍNH CHỈ SỐ KPI TỔNG QUAN
            int tinDangDangCount = allCompanyJobs.Count(j => j.TrangThai == 1 && j.NgayHetHan >= DateTime.Now);

            var allApplications = await _context.DonUngTuyens
                .Where(a => maViTriList.Contains(a.MaViTri))
                .ToListAsync();

            int hoSoMoiCount = allApplications.Count(a => a.TrangThai == 0);
            int tongCvNopCount = allApplications.Count;
            int tongLuotXemCount = allCompanyJobs.Sum(j => j.LuotXem);

            double tyLeChuyenDoi = tongLuotXemCount > 0
                ? Math.Round(((double)tongCvNopCount / tongLuotXemCount) * 100, 2)
                : 0;

            int luotXemCvConLai = 0; // Lấy từ bảng gói dịch vụ / đặc quyền người dùng nếu có

            // 3. LỌC DỮ LIỆU THEO KHOẢNG THỜI GIAN CHỌN
            var viewsLogs = await _context.LichSuXemTins
                .Where(v => maTinList.Contains(v.MaTin) && v.ThoiGianXem >= start && v.ThoiGianXem <= end)
                .ToListAsync();

            var rangeApplications = allApplications.Where(a => a.NgayNop >= start && a.NgayNop <= end).ToList();

            // 4. THUẬT TOÁN GOM NHÓM THÔNG MINH CHO BIỂU ĐỒ XU HƯỚNG
            var dailyTrends = new List<DailyTrendItemDto>();

            if (totalDays <= 60)
            {
                // Khoảng ngắn: Gom theo TỪNG NGÀY
                for (DateTime date = start.Date; date <= end.Date; date = date.AddDays(1))
                {
                    dailyTrends.Add(new DailyTrendItemDto
                    {
                        Date = date.ToString("dd/MM"),
                        Views = viewsLogs.Count(v => v.ThoiGianXem.Date == date),
                        Applications = rangeApplications.Count(a => a.NgayNop.Date == date)
                    });
                }
            }
            else
            {
                // Khoảng dài (6 tháng, 1 năm): Gom theo TỪNG THÁNG
                DateTime curr = new DateTime(start.Year, start.Month, 1);
                while (curr <= end.Date)
                {
                    DateTime monthEnd = curr.AddMonths(1).AddDays(-1);
                    if (monthEnd > end) monthEnd = end;

                    dailyTrends.Add(new DailyTrendItemDto
                    {
                        Date = curr.ToString("MM/yyyy"),
                        Views = viewsLogs.Count(v => v.ThoiGianXem.Date >= curr && v.ThoiGianXem.Date <= monthEnd),
                        Applications = rangeApplications.Count(a => a.NgayNop.Date >= curr && a.NgayNop.Date <= monthEnd)
                    });

                    curr = curr.AddMonths(1);
                }
            }

            // 5. TRẠNG THÁI HỒ SƠ
            var statusDistribution = new List<StatusDistributionItemDto>
            {
                new() { StatusName = "Chờ duyệt", Count = rangeApplications.Count(a => a.TrangThai == 0) },
                new() { StatusName = "Đã duyệt", Count = rangeApplications.Count(a => a.TrangThai == 1) },
                new() { StatusName = "Hẹn phỏng vấn", Count = rangeApplications.Count(a => a.TrangThai == 2) },
                new() { StatusName = "Trúng tuyển", Count = rangeApplications.Count(a => a.TrangThai == 3) },
                new() { StatusName = "Từ chối", Count = rangeApplications.Count(a => a.TrangThai == 4) }
            };

            // 6. TOP TIN TUYỂN DỤNG
            var topJobs = allCompanyJobs
                .Select(j => new TopJobItemDto
                {
                    MaTin = j.MaTin,
                    TieuDe = j.TieuDeChienDich,
                    LuotXem = j.LuotXem,
                    SoCvNop = rangeApplications.Count(a => j.ChiTietViTris.Select(v => v.MaViTri).Contains(a.MaViTri)),
                    TrangThai = j.TrangThai,
                    NgayDang = j.NgayDang
                })
                .OrderByDescending(j => j.SoCvNop)
                .ThenByDescending(j => j.LuotXem)
                .Take(5)
                .ToList();

            return Ok(new DashboardAnalyticsDto
            {
                Summary = new SummaryKpiDto
                {
                    TinDangDang = tinDangDangCount,
                    HoSoMoiChuaDuyet = hoSoMoiCount,
                    LuotXemCvConLai = luotXemCvConLai,
                    TongLuotXemTin = tongLuotXemCount,
                    TongCvNop = tongCvNopCount,
                    TyLeChuyenDoi = tyLeChuyenDoi
                },
                Charts = new ChartsDataDto
                {
                    DailyTrends = dailyTrends,
                    StatusDistribution = statusDistribution
                },
                TopJobs = topJobs
            });
        }

        // 1. API LẤY SỐ LƯỢT XEM CV + TRẠNG THÁI HẠN GÓI
        [HttpGet("cv-credits")]
        public async Task<IActionResult> GetCvCredits()
        {
            try
            {
                int currentEmployerId = GetCurrentUserId();
                var user = await _context.Users.FindAsync(currentEmployerId);

                // Kiểm tra xem gói đã hết hạn chưa
                bool isExpired = user?.NgayHetHanGoi == null || user.NgayHetHanGoi.Value.Date < DateTime.Now.Date;
                return Ok(new
                {
                    success = true,
                    luotXemCvConLai = user?.LuotXemCvConLai ?? 0,
                    isExpired = isExpired
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, luotXemCvConLai = 0, isExpired = true, error = ex.Message });
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
                    .Include(c => c.NganhNghe)
                    .Where(c => (c.IsPublic == true) && !appliedCvIds.Contains(c.MaCv))
                    .AsQueryable();

                // 3. LỌC THEO TỪ KHÓA CHÍNH
                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string kw = keyword.Trim().ToLower();
                    query = query.Where(c =>
                        (c.TieuDe != null && c.TieuDe.ToLower().Contains(kw)) ||
                        (c.MaUserNavigation != null && c.MaUserNavigation.HoTen.ToLower().Contains(kw)) ||
                        (c.DuLieuCv != null && c.DuLieuCv.ToLower().Contains(kw))
                    );
                }

                // 4. LỌC THEO NGÀNH NGHỀ
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

                // 5. LỌC THEO KỸ NĂNG
                if (!string.IsNullOrWhiteSpace(skills))
                {
                    string sk = skills.Trim().ToLower();
                    query = query.Where(c =>
                        (c.TieuDe != null && c.TieuDe.ToLower().Contains(sk)) ||
                        (c.DuLieuCv != null && c.DuLieuCv.ToLower().Contains(sk))
                    );
                }

                var rawList = await query
                    .OrderByDescending(c => c.NgayCapNhat)
                    .ThenByDescending(c => c.MaCv)
                    .ToListAsync();

                // 6. Lấy danh sách ID các CV mà Nhà tuyển dụng này ĐÃ MỞ KHÓA
                var unlockedCvIds = await _context.LichSuMoKhoaCvs
                    .Where(l => l.MaUser == currentEmployerId)
                    .Select(l => l.MaCv)
                    .ToListAsync();

                // 🌟 7. LẤY DANH SÁCH CV ĐÃ LƯU VÀ GHI CHÚ CỦA NTD NÀY
                var savedCandidatesMap = await _context.UngVienDaLuus
                    .Where(u => u.MaUser == currentEmployerId)
                    .ToDictionaryAsync(u => u.MaCv, u => u.GhiChuCaNhan);

                var result = rawList.Select(c => {
                    bool isUnlocked = unlockedCvIds.Contains(c.MaCv);
                    bool isSaved = savedCandidatesMap.ContainsKey(c.MaCv);
                    string? ghiChu = isSaved ? savedCandidatesMap[c.MaCv] : null;

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
                        catch { }
                    }

                    return new
                    {
                        maCv = c.MaCv,
                        maUser = c.MaUser,
                        hoTen = c.MaUserNavigation?.HoTen ?? "Ứng viên ẩn danh",
                        jobTitle = !string.IsNullOrWhiteSpace(extractedJobTitle)
                            ? extractedJobTitle
                            : (!string.IsNullOrWhiteSpace(c.TieuDe) ? c.TieuDe : "Chưa cập nhật vị trí"),
                        email = isUnlocked ? c.MaUserNavigation?.Email : "••••••••@gmail.com",
                        tieuDe = c.TieuDe ?? "Hồ sơ ứng viên",
                        tenNganh = c.NganhNghe?.TenNganh ?? "Chưa phân loại",
                        cvUrl = c.DuongDan,
                        isUnlocked = isUnlocked,
                        isSaved = isSaved, // ✨ Flag đã lưu chưa
                        ghiChuCaNhan = ghiChu, // ✨ Ghi chú cá nhân của NTD
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

        // 🌟 API ĐÁNH DẤU / BỎ ĐÁNH DẤU LƯU ỨNG VIÊN
        [HttpPost("toggle-save-candidate")]
        public async Task<IActionResult> ToggleSaveCandidate([FromBody] SaveCandidateDto dto)
        {
            try
            {
                int currentUserId = GetCurrentUserId();

                // 🌟 1. RÀNG BUỘC NGHIỆP VỤ: Kiểm tra xem NTD đã mở khóa CV này chưa
                bool isUnlocked = await _context.LichSuMoKhoaCvs
                    .AnyAsync(l => l.MaUser == currentUserId && l.MaCv == dto.MaCv);

                if (!isUnlocked)
                {
                    return BadRequest(new { success = false, message = "Bạn phải mở khóa liên hệ CV này trước khi lưu vào danh sách ưng ý!" });
                }

                // 2. Thực hiện Toggle lưu / bỏ lưu
                var existing = await _context.UngVienDaLuus
                    .FirstOrDefaultAsync(u => u.MaUser == currentUserId && u.MaCv == dto.MaCv);

                if (existing != null)
                {
                    _context.UngVienDaLuus.Remove(existing);
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, isSaved = false, message = "Đã bỏ lưu ứng viên!" });
                }
                else
                {
                    var newItem = new UngVienDaLuu
                    {
                        MaUser = currentUserId,
                        MaCv = dto.MaCv,
                        GhiChuCaNhan = dto.GhiChuCaNhan,
                        NgayLuu = DateTime.Now
                    };
                    _context.UngVienDaLuus.Add(newItem);
                    await _context.SaveChangesAsync();
                    return Ok(new { success = true, isSaved = true, message = "Đã lưu ứng viên vào danh sách ưng ý!" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xử lý lưu ứng viên!", error = ex.Message });
            }
        }

        // API CẬP NHẬT GHI CHÚ RIÊNG CHO ỨNG VIÊN ĐÃ LƯU
        [HttpPut("update-candidate-note")]
        public async Task<IActionResult> UpdateCandidateNote([FromBody] SaveCandidateDto dto)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var item = await _context.UngVienDaLuus
                    .FirstOrDefaultAsync(u => u.MaUser == currentUserId && u.MaCv == dto.MaCv);

                if (item == null)
                {
                    return NotFound(new { success = false, message = "Ứng viên này chưa được lưu trong danh sách!" });
                }

                item.GhiChuCaNhan = dto.GhiChuCaNhan;
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Cập nhật ghi chú thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi lưu ghi chú!", error = ex.Message });
            }
        }


        // 2. API MỞ KHÓA THÔNG TIN LIÊN HỆ CV (CHUẨN HÓA JSON RESPONSES)
        [HttpPost("unlock-cv/{maCv}")]
        public async Task<IActionResult> UnlockCv(int maCv)
        {
            int currentEmployerId = GetCurrentUserId();
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(currentEmployerId);
                if (user == null) return Unauthorized(new { success = false, message = "Phiên đăng nhập hết hạn." });

                bool isUserValid = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi.Value.Date >= DateTime.Now.Date;

                if (!isUserValid || user.LuotXemCvConLai <= 0)
                {
                    // 🌟 Trả về JSON chuẩn để Frontend không bị crash
                    return BadRequest(new
                    {
                        success = false,
                        message = "Gói dịch vụ đã hết hạn sử dụng hoặc tài khoản đã hết lượt mở khóa hồ sơ!"
                    });
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

        [HttpPatch("jobs/{maTin}/promote")]
        public async Task<IActionResult> PromoteJob(int maTin)
        {
            int currentUserId = GetCurrentUserId();

            // 1. Kiểm tra đặc quyền NTD_VIP_JOB
            bool isVipActive = await _context.UserDacQuyens
                .Include(ud => ud.DacQuyen)
                .AnyAsync(ud => ud.MaUser == currentUserId
                             && ud.NgayHetHan.Date >= DateTime.Now.Date
                             && ud.DacQuyen != null
                             && ud.DacQuyen.MaCode == "NTD_VIP_JOB");

            if (!isVipActive)
            {
                return BadRequest(new { success = false, message = "Tài khoản của bạn chưa đăng ký đặc quyền Đẩy tin VIP!" });
            }

            // 2. Tìm tin đăng thuộc sở hữu của doanh nghiệp
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == currentUserId);
            var job = await _context.TinTuyenDungs.FirstOrDefaultAsync(j => j.MaTin == maTin && j.MaCongTy == company.MaCongTy);

            if (job == null) return NotFound(new { message = "Không tìm thấy tin đăng!" });

            job.IsPromoted = true;
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Đã nâng cấp tin thành tin VIP Nổi bật!" });
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