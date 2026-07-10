using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using TKVL.DTOs.Company; // Nơi chứa CandidateDto và UpdateStatusDto
using TKVL.Models;
using TKVL.Services;

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
                logo = company.Logo
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
                .FirstOrDefaultAsync(d => d.MaDon == maDon);

            if (donUngTuyen == null) return NotFound("Không tìm thấy đơn ứng tuyển.");

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
                    string chuDe = $"[{company.TenCongTy}] Thư mời phỏng vấn - Vị trí {tenViTri}";

                    // 1. Đọc mẫu Email tùy chỉnh do công ty tự up/soạn trong DB
                    // Nếu công ty chưa cấu hình mẫu riêng, hệ thống sẽ tự động dùng mẫu mặc định bên dưới
                    string mauEmailTemplate = !string.IsNullOrEmpty(company.MauEmailInterview)
                        ? company.MauEmailInterview
                        : @"<div style='font-family: Arial; line-height: 1.6;'>
                            <p>Chào {TenUngVien},</p>
                            <p>Chúng tôi trân trọng mời bạn tham gia phỏng vấn vị trí <strong>{TenViTri}</strong>.</p>
                            <p>• Thời gian: {ThoiGian}</p>
                            <p>• Địa điểm: {DiaDiem}</p>
                            <p>Trân trọng,</p>
                            <p><strong>{TenCongTy}</strong></p>
                        </div>";

                    // 2. Thực hiện quét và thay thế các từ khóa quy ước bằng dữ liệu thực tế
                    string noiDungGuiDi = mauEmailTemplate
                        .Replace("{TenUngVien}", tenUngVien)
                        .Replace("{TenViTri}", tenViTri)
                        .Replace("{ThoiGian}", request.ThoiGian ?? "Sẽ thông báo sau")
                        .Replace("{DiaDiem}", request.DiaDiem ?? "Sẽ thông báo sau")
                        .Replace("{TenCongTy}", company.TenCongTy);

                    // 3. Bắn email đã trộn nội dung đi
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

        [HttpGet("hunt-cv")]
        public async Task<IActionResult> HuntCv(string keyword = "")
        {
            int currentEmployerId = GetCurrentUserId();

            // 1. Lấy danh sách CV công khai
            var query = _context.Cvs.Include(c => c.MaUserNavigation).Where(c => c.IsPublic == true);
            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(c => c.TieuDe.Contains(keyword));

            var cvList = await query.ToListAsync();

            // 2. Kiểm tra xem Employer đã mở khóa CV nào chưa
            var unlockedCvIds = await _context.LichSuMoKhoaCvs
                .Where(l => l.MaUser == currentEmployerId)
                .Select(l => l.MaCv)
                .ToListAsync();

            var results = cvList.Select(c => new HuntCvDto
            {
                MaCv = c.MaCv,
                HoTen = c.MaUserNavigation.HoTen,
                IsUnlocked = unlockedCvIds.Contains(c.MaCv),
                // Nếu chưa mở khóa thì che mờ dữ liệu
                Email = unlockedCvIds.Contains(c.MaCv) ? c.MaUserNavigation.Email : "nguyen***@gmail.com",
                SoDienThoai = unlockedCvIds.Contains(c.MaCv) ? "0912***678" : "0912***678"
            }).ToList();

            return Ok(results);
        }

        [HttpPost("unlock-cv/{maCv}")]
        public async Task<IActionResult> UnlockCv(int maCv)
        {
            int currentEmployerId = GetCurrentUserId();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FindAsync(currentEmployerId);
                if (user.LuotXemCvConLai <= 0) return BadRequest("Bạn đã hết lượt mở khóa CV!");

                // Trừ lượt
                user.LuotXemCvConLai -= 1;

                // Lưu vết mở khóa
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
            catch
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Lỗi hệ thống.");
            }
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