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

            bool isMstExisted = await _context.CongTies.AnyAsync(c => c.MaSoThue == targetMst && c.MaUser != maUser);
            if (isMstExisted)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Mã số thuế '{targetMst}' đã được đăng ký bởi một doanh nghiệp khác trên hệ thống!"
                });
            }

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

        [HttpGet("check-subscription")]
        public async Task<IActionResult> CheckSubscription()
        {
            int currentEmployerId = GetCurrentUserId();
            var user = await _context.Users.FindAsync(currentEmployerId);
            if (user == null) return Unauthorized(new { isPremium = false });

            bool hasAiFeature = await _context.UserDacQuyens
                .Include(ud => ud.DacQuyen)
                .AnyAsync(ud => ud.MaUser == currentEmployerId
                             && ud.NgayHetHan.Date >= DateTime.Now.Date
                             && ud.DacQuyen != null
                             && ud.DacQuyen.MaCode == "NTD_AI_MATCHING");

            return Ok(new { isPremium = hasAiFeature });
        }

        [HttpGet("jobs/{maViTri}/candidates")]
        [Authorize(Roles = "1")]
        public async Task<IActionResult> GetCandidatesByJob(int maViTri, [FromQuery] int? viTriId = null)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");
            if (userIdClaim == null) return Unauthorized(new { message = "Vui lòng đăng nhập!" });
            int maUser = int.Parse(userIdClaim.Value);

            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
            if (company == null)
                return BadRequest(new { message = "Không tìm thấy thông tin công ty." });

            // 🌟 TÌM CHIẾN DỊCH: Nhận diện cả khi maViTri là MaTin hoặc là một trong các MaViTri con
            var tinTuyenDung = await _context.TinTuyenDungs
                .Include(t => t.ChiTietViTris)
                .FirstOrDefaultAsync(b => (b.MaTin == maViTri || b.ChiTietViTris.Any(c => c.MaViTri == maViTri)) && b.MaCongTy == company.MaCongTy);

            if (tinTuyenDung == null)
                return NotFound(new { message = "Không tìm thấy bài tuyển dụng hoặc bạn không có quyền truy cập." });

            // Lấy tất cả mã vị trí thuộc chiến dịch này
            var allViTriIds = tinTuyenDung.ChiTietViTris.Select(v => v.MaViTri).ToList();

            var query = _context.DonUngTuyens
                .Include(d => d.MaCvNavigation)
                    .ThenInclude(u => u.MaUserNavigation)
                .Include(d => d.ChiTietPhanTichAi)
                .Include(d => d.MaViTriNavigation)
                .Where(d => allViTriIds.Contains(d.MaViTri));

            // Nếu NTD chọn lọc riêng theo 1 vị trí con
            if (viTriId.HasValue && viTriId.Value > 0)
            {
                query = query.Where(d => d.MaViTri == viTriId.Value);
            }

            var rawCandidates = await query
                .OrderByDescending(d => d.NgayNop)
                .ToListAsync();

            var candidates = rawCandidates.Select(d => new
            {
                maDon = d.MaDon,
                maUngVien = d.MaCvNavigation != null ? d.MaCvNavigation.MaUser : 0,
                hoTen = d.MaCvNavigation?.MaUserNavigation?.HoTen ?? "Ứng viên",
                email = d.MaCvNavigation?.MaUserNavigation?.Email ?? "N/A",
                ngayNop = d.NgayNop,
                trangThai = d.TrangThai,
                cvUrl = d.MaCvNavigation?.DuLieuCv,
                maViTri = d.MaViTri,
                tenViTri = d.MaViTriNavigation?.TenViTri ?? "Vị trí tuyển dụng",
                isPendingAi = d.ChiTietPhanTichAi == null,
                diemMatchingTong = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemMatchingTong) : 0,
                diemKyNang = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemKyNang) : 0,
                diemKinhNghiem = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemKinhNghiem) : 0,
                diemLinhVuc = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemLinhVuc) : 0,
                diemCapBac = d.ChiTietPhanTichAi != null ? (int)Math.Round((double)d.ChiTietPhanTichAi.DiemCapBac) : 0,
                diemManhTieuBieu = d.ChiTietPhanTichAi != null ? d.ChiTietPhanTichAi.DiemManhTieuBieu : null,
                diemConThieu = d.ChiTietPhanTichAi != null ? d.ChiTietPhanTichAi.DiemConThieu : null
            }).ToList();

            // Danh sách các vị trí con trong chiến dịch kèm số lượng hồ sơ
            var positions = tinTuyenDung.ChiTietViTris.Select(v => new
            {
                maViTri = v.MaViTri,
                tenViTri = v.TenViTri,
                capBac = v.CapBac,
                soLuongTuyen = v.SoLuongTuyen,
                soLuongUngVien = rawCandidates.Count(c => c.MaViTri == v.MaViTri)
            }).ToList();

            return Ok(new
            {
                success = true,
                tieuDeChienDich = tinTuyenDung.TieuDeChienDich,
                maTin = tinTuyenDung.MaTin,
                positions = positions,
                data = candidates
            });
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

                    string thongDiepGoc = !string.IsNullOrEmpty(company.MauEmailInterview)
                        ? company.MauEmailInterview
                        : "Chào {TenUngVien},\n\nCông ty {TenCongTy} trân trọng mời bạn tham gia phỏng vấn vị trí {TenViTri}.\n• Thời gian: {ThoiGian}\n• Địa điểm: {DiaDiem}\n\n{LinkBaiTest}\n\nTrân trọng,\n{ChuKyEmail}";

                    string thongDiepHtml = thongDiepGoc.Replace("\n", "<br/>");

                    string testLinkHtml = "";
                    if (!string.IsNullOrEmpty(request.LinkBaiTest))
                    {
                        testLinkHtml = $"<a href='{request.LinkBaiTest}' target='_blank' style='background-color: #10b981; color: #ffffff; padding: 6px 14px; text-decoration: none; display: inline-block; font-size: 13px; font-weight: bold; border-radius: 4px; margin: 0 4px; box-shadow: 0 2px 4px rgba(16,185,129,0.15);'>BẮT ĐẦU LÀM BÀI TEST</a>";
                    }

                    string chuKyHtml = !string.IsNullOrEmpty(company.ChuKyEmail)
                        ? $"<div style='margin-top: 20px; padding-top: 12px; border-top: 1px dashed #cbd5e1; color: #475569; font-size: 13px;'>{company.ChuKyEmail.Replace("\n", "<br/>")}</div>"
                        : "";

                    string bodyText = thongDiepHtml
                        .Replace("{TenUngVien}", tenUngVien)
                        .Replace("{TenViTri}", tenViTri)
                        .Replace("{ThoiGian}", request.ThoiGian ?? "Sẽ thông báo sau")
                        .Replace("{DiaDiem}", request.DiaDiem ?? "Sẽ thông báo sau")
                        .Replace("{TenCongTy}", company.TenCongTy);

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

            // 🌟 Sửa đổi: ThenInclude đổi sang MaNganhConNavigation
            var rawJobs = await _context.TinTuyenDungs
                .Include(t => t.ChiTietViTris)
                    .ThenInclude(v => v.MaNganhConNavigation)
                .Include(t => t.ChiTietViTris)
                    .ThenInclude(v => v.MaPhuongNavigation)
                        .ThenInclude(p => p.MaTpNavigation)
                .Include(t => t.ChiTietViTris)
                    .ThenInclude(v => v.DonUngTuyens)
                .Where(t => t.MaCongTy == company.MaCongTy)
                .OrderByDescending(t => t.MaTin)
                .ToListAsync();

            var myJobs = rawJobs.Select(t => new
            {
                maTin = t.MaTin,
                maViTri = t.ChiTietViTris.Select(v => (int?)v.MaViTri).FirstOrDefault() ?? t.MaTin,
                tieuDe = t.TieuDeChienDich,
                ngayTao = t.NgayDang,
                hanNop = t.NgayHetHan,
                trangThai = t.TrangThai,
                isPromoted = t.IsPromoted,
                soLuongUngVien = t.ChiTietViTris.SelectMany(v => v.DonUngTuyens ?? new List<DonUngTuyen>()).Count(),

                // 🌟 Sửa đổi: MaNganh -> MaNganhCon
                danhSachMaNganh = t.ChiTietViTris.Select(v => v.MaNganhCon).Distinct().ToList(),
                danhSachNganhObj = t.ChiTietViTris
                    .Where(v => v.MaNganhConNavigation != null)
                    .Select(v => new { id = v.MaNganhCon, name = v.MaNganhConNavigation.TenNganhCon })
                    .GroupBy(x => x.id)
                    .Select(g => g.First())
                    .ToList(),
                tenNganhNghe = string.Join(", ", t.ChiTietViTris
                    .Where(v => v.MaNganhConNavigation != null)
                    .Select(v => v.MaNganhConNavigation.TenNganhCon)
                    .Distinct()),

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

            if (job.TrangThai == 1)
            {
                job.TrangThai = 2;
            }
            else if (job.TrangThai == 2)
            {
                if (job.NgayHetHan < DateTime.Now)
                {
                    return BadRequest(new { success = false, message = "Tin tuyển dụng đã quá hạn! Vui lòng gia hạn ngày trước khi bật lại." });
                }
                job.TrangThai = 1;
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

            DateTime end = endDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.Now;
            DateTime start = startDate?.Date ?? DateTime.Now.Date.AddDays(-days + 1);
            int totalDays = (end - start).Days + 1;

            var allCompanyJobs = await _context.TinTuyenDungs
                .Include(j => j.ChiTietViTris)
                .Where(j => j.MaCongTy == maCongTy)
                .ToListAsync();

            var maTinList = allCompanyJobs.Select(j => j.MaTin).ToList();
            var maViTriList = allCompanyJobs.SelectMany(j => j.ChiTietViTris).Select(v => v.MaViTri).ToList();

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

            int luotXemCvConLai = 0;

            var viewsLogs = await _context.LichSuXemTins
                .Where(v => maTinList.Contains(v.MaTin) && v.ThoiGianXem >= start && v.ThoiGianXem <= end)
                .ToListAsync();

            var rangeApplications = allApplications.Where(a => a.NgayNop >= start && a.NgayNop <= end).ToList();

            var dailyTrends = new List<DailyTrendItemDto>();

            if (totalDays <= 60)
            {
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

            var statusDistribution = new List<StatusDistributionItemDto>
            {
                new() { StatusName = "Chờ duyệt", Count = rangeApplications.Count(a => a.TrangThai == 0) },
                new() { StatusName = "Đã duyệt", Count = rangeApplications.Count(a => a.TrangThai == 1) },
                new() { StatusName = "Hẹn phỏng vấn", Count = rangeApplications.Count(a => a.TrangThai == 2) },
                new() { StatusName = "Trúng tuyển", Count = rangeApplications.Count(a => a.TrangThai == 3) },
                new() { StatusName = "Từ chối", Count = rangeApplications.Count(a => a.TrangThai == 4) }
            };

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

        [HttpGet("cv-credits")]
        public async Task<IActionResult> GetCvCredits()
        {
            try
            {
                int currentEmployerId = GetCurrentUserId();
                var user = await _context.Users.FindAsync(currentEmployerId);

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

                // 🌟 Sửa đổi: Include sang NganhNgheCon
                var query = _context.Cvs
                    .Include(c => c.MaUserNavigation)
                    .Include(c => c.NganhNgheCon)
                    .Where(c => (c.IsPublic == true) && !appliedCvIds.Contains(c.MaCv))
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(keyword))
                {
                    string kw = keyword.Trim().ToLower();
                    query = query.Where(c =>
                        (c.TieuDe != null && c.TieuDe.ToLower().Contains(kw)) ||
                        (c.MaUserNavigation != null && c.MaUserNavigation.HoTen.ToLower().Contains(kw)) ||
                        (c.DuLieuCv != null && c.DuLieuCv.ToLower().Contains(kw))
                    );
                }

                string? targetIndustry = (nganhNghe == "Khác" && !string.IsNullOrWhiteSpace(nganhNgheKhac))
                    ? nganhNgheKhac
                    : nganhNghe;

                // 🌟 Sửa đổi: NganhNghe -> NganhNgheCon
                if (!string.IsNullOrWhiteSpace(targetIndustry) && targetIndustry != "Khác")
                {
                    string ind = targetIndustry.Trim().ToLower();
                    query = query.Where(c =>
                        (c.NganhNgheCon != null && c.NganhNgheCon.TenNganhCon.ToLower().Contains(ind)) ||
                        (c.TieuDe != null && c.TieuDe.ToLower().Contains(ind))
                    );
                }

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

                var unlockedCvIds = await _context.LichSuMoKhoaCvs
                    .Where(l => l.MaUser == currentEmployerId)
                    .Select(l => l.MaCv)
                    .ToListAsync();

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
                        tenNganh = c.NganhNgheCon?.TenNganhCon ?? "Chưa phân loại", // 🌟 Sửa đổi: TenNganhCon
                        cvUrl = c.DuongDan,
                        isUnlocked = isUnlocked,
                        isSaved = isSaved,
                        ghiChuCaNhan = ghiChu,
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

        [HttpPost("toggle-save-candidate")]
        public async Task<IActionResult> ToggleSaveCandidate([FromBody] SaveCandidateDto dto)
        {
            try
            {
                int currentUserId = GetCurrentUserId();

                bool isUnlocked = await _context.LichSuMoKhoaCvs
                    .AnyAsync(l => l.MaUser == currentUserId && l.MaCv == dto.MaCv);

                if (!isUnlocked)
                {
                    return BadRequest(new { success = false, message = "Bạn phải mở khóa liên hệ CV này trước khi lưu vào danh sách ưng ý!" });
                }

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
            // 🌟 Sửa đổi: Truy vấn từ bảng NganhNgheCons thay cho NganhNghes
            var industries = await _context.NganhNgheCons
                .OrderBy(n => n.MaNganhCon)
                .Select(n => n.TenNganhCon)
                .ToListAsync();

            if (!industries.Contains("Khác"))
            {
                industries.Add("Khác");
            }

            return Ok(industries);
        }

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

            return 0;
        }
    }
}