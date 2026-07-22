using CloudinaryDotNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TKVL.DTOs.Company; // Nơi chứa CandidateDto và UpdateStatusDto
using TKVL.Models;
using TKVL.Services;
using static System.Net.Mime.MediaTypeNames;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    // Cập nhật: Phân quyền trực tiếp Role = 1 (Nhà tuyển dụng) cho toàn bộ Controller
    [Authorize(Roles = "1")]
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

        private bool IsValidMaSoThue(string mst)
        {
            if (string.IsNullOrWhiteSpace(mst)) return false;
            var regex = new Regex(@"^\d{10}(-\d{3})?$");
            return regex.IsMatch(mst.Trim());
        }

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
                chuKyEmail = company.ChuKyEmail,
                mauEmailInterview = company.MauEmailInterview
            });
        }

        [HttpPost("company")]
        public async Task<IActionResult> UpdateCompanyProfile([FromForm] CompanyProfileDto dto)
        {
            int maUser = GetCurrentUserId();

            if (!IsValidMaSoThue(dto.MaSoThue))
            {
                return BadRequest(new { success = false, message = "Mã số thuế không hợp lệ!" });
            }

            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
            bool isNew = false;

            if (company == null)
            {
                company = new CongTy { MaUser = maUser };
                isNew = true;
            }

            company.TenCongTy = dto.TenCongTy;
            company.MaSoThue = dto.MaSoThue.Trim();
            company.QuyMo = dto.QuyMo;
            company.DiaChi = dto.DiaChi;
            company.MoTa = dto.MoTa;
            company.TrangThai = false;
            company.ChuKyEmail = dto.ChuKyEmail;
            company.MauEmailInterview = dto.MauEmailInterview;

            if (dto.LogoFile != null)
            {
                string logoUrl = await _cloudinaryService.UploadImageAsync(dto.LogoFile);
                if (!string.IsNullOrEmpty(logoUrl))
                {
                    company.Logo = logoUrl;
                }
            }

            try
            {
                if (isNew) _context.CongTies.Add(company);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Lưu hồ sơ thành công, chờ kiểm duyệt." });
            }
            catch (DbUpdateException ex)
            {
                Console.WriteLine($"[LOI LUU DATABASE HO SO DOANH NGHIEP]: {ex.InnerException?.Message ?? ex.Message}");
                if (ex.InnerException != null && ex.InnerException.Message.Contains("UQ__CongTy__"))
                {
                    return BadRequest(new { success = false, message = "Mã số thuế này đã bị trùng!" });
                }
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống." });
            }
        }

        // ===================================================================
        // LUỒNG 2: QUẢN LÝ PHỄU ỨNG VIÊN
        // ===================================================================

        [HttpGet("jobs/{maViTri}/candidates")]
        public async Task<IActionResult> GetCandidates(int maViTri)
        {
            // JOIN các bảng: DonUngTuyen -> CV -> User để lấy đủ thông tin
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
                    GhiChu = d.GhiChu
                })
                .OrderByDescending(d => d.NgayNop)
                .ToListAsync();

            return Ok(candidates);
        }

        [HttpPut("applications/{maDon}/status")]
        public async Task<IActionResult> UpdateApplicationStatus(int maDon, [FromBody] UpdateStatusDto request)
        {
            // Tìm đơn ứng tuyển kèm theo thông tin User của ứng viên và thông tin Vị trí công việc
            var donUngTuyen = await _context.DonUngTuyens
                .Include(d => d.MaCvNavigation)
                    .ThenInclude(cv => cv.MaUserNavigation)
                .Include(d => d.MaViTriNavigation)
                    .ThenInclude(v => v.MaTinNavigation) 
                .FirstOrDefaultAsync(d => d.MaDon == maDon);

            if (donUngTuyen == null) return NotFound("Không tìm thấy đơn ứng tuyển.");

            // Nếu trạng thái mới truyền lên (request.Status) nhỏ hơn trạng thái hiện tại (application.TrangThai)
            // Ngoại trừ trường hợp sửa sai: Đi lùi từ Từ chối (3) sang Hẹn phỏng vấn (2) để gửi lại email
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

            // Kiểm tra nếu chuyển trạng thái sang Hẹn phỏng vấn (mã 2)
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

                    // GIẢI QUYẾT TẬN GỐC: Đổi toàn bộ dấu Enter (\n) thành thẻ xuống dòng HTML (<br/>) để giữ nguyên định dạng của NTD
                    string thongDiepHtml = thongDiepGoc.Replace("\n", "<br/>");

                    // 3. Chuẩn hóa nút bấm làm bài Test dạng Inline (Nằm vừa khít trong câu văn của NTD)
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
                        // Nếu NTD quên dán từ khóa mà đơn có link test -> Tự động append xuống cuối thư cho an toàn
                        bodyText += "<br/><br/>" + testLinkHtml;
                    }

                    // Xử lý vị trí đặt từ khóa {ChuKyEmail} tương tự
                    if (bodyText.Contains("{ChuKyEmail}"))
                    {
                        bodyText = bodyText.Replace("{ChuKyEmail}", chuKyHtml);
                    }
                    else
                    {
                        bodyText += chuKyHtml;
                    }

                    // 7. Nhét toàn bộ phần ruột đã trộn xong xuôi vào Khung layout tổng
                    string noiDungGuiDi = masterLayout.Replace("{NoiDungThuCuaDoanhNghiep}", bodyText);

                    // Bắn Email HTML hoàn thiện đi
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

            // Tìm mã công ty của user đang đăng nhập
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);

            // KỊCH BẢN 1: Nhà tuyển dụng chưa từng tạo hồ sơ công ty
            if (company == null)
            {
                return Ok(new { status = "NO_PROFILE", message = "Cần khởi tạo hồ sơ công ty trước khi quản lý tin đăng." });
            }

            // KỊCH BẢN 2: Hồ sơ đã tạo nhưng đang ở trạng thái chờ duyệt (TrangThai == false)
            if (company.TrangThai == false) // 0: Chờ duyệt, 1: Đã duyệt
            {
                return Ok(new { status = "PENDING_APPROVAL", message = "Hồ sơ doanh nghiệp đang chờ duyệt. Vui lòng quay lại sau." });
            }

            // KỊCH BẢN 3: Hồ sơ hợp lệ, tiến hành lấy danh sách tin tuyển dụng như cũ
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

            // Trả về kèm cờ trạng thái SUCCESS để Frontend nhận diện dữ liệu
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

            // Xác thực hạn dùng của gói Premium trên tài khoản tuyển dụng
            if (!user.NgayHetHanGoi.HasValue || user.NgayHetHanGoi.Value < DateTime.Now)  
            {
                return Ok(new { success = false, isPremium = false, luotXemCvConLai = 0, data = new List<HuntCvDto>(), message = "Gói dịch vụ tìm ứng viên đã hết hạn." });  
            }

            // Khởi tạo luồng truy vấn gốc trên bảng CV công khai
            var query = _context.Cvs.Include(c => c.MaUserNavigation).Where(c => c.IsPublic == true);  

            // 1. Lọc theo Từ khóa chính (Khớp tên ứng viên hoặc tiêu đề CV)
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(c => c.TieuDe.Contains(keyword) || c.MaUserNavigation.HoTen.Contains(keyword));

            // 2. Lọc theo Kỹ năng / Công nghệ (Quét trực tiếp trong chuỗi JSON của CV)
            if (!string.IsNullOrEmpty(skills))
                query = query.Where(c => c.DuLieuCv.Contains(skills));

            // 3. Lọc theo Danh mục ngành nghề (Xử lý kịch bản chọn ngành nghề cụ thể hoặc tự nhập từ khóa Khác)
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

            // Loại bỏ các CV đã ứng tuyển vào công ty này để tiết kiệm lượt mở khóa
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

        // API lấy danh sách tất cả các ngành nghề động từ bảng NganhNghe trong DB
        [HttpGet("hunt-cv/industries")]
        public async Task<IActionResult> GetDatabaseIndustries()
        {
            // Truy vấn lấy danh sách tên ngành nghề, sắp xếp theo mã ngành (hoặc thứ tự trong DB)
            // Lưu ý: Thay đổi "TenNganh" hoặc "MaNganh" thành đúng tên thuộc tính trong Model của bạn nếu có khác biệt
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