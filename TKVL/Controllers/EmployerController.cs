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

        public EmployerController(JobPortalDbContext context, ICloudinaryService cloudinaryService)
        {
            _context = context;
            _cloudinaryService = cloudinaryService;
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
            company.TrangThai = false; // Đưa về trạng thái chờ duyệt

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
            var donUngTuyen = await _context.DonUngTuyens.FindAsync(maDon);
            if (donUngTuyen == null) return NotFound("Không tìm thấy đơn ứng tuyển.");

            // Cập nhật trạng thái
            donUngTuyen.TrangThai = request.Status;

            // Cập nhật ghi chú nếu Nhà tuyển dụng có gõ vào Modal
            if (request.GhiChu != null)
            {
                donUngTuyen.GhiChu = request.GhiChu;
            }

            await _context.SaveChangesAsync();

            // Ghi chú: Nếu Status == 2 (Hẹn PV) hoặc 3 (Từ chối), bạn có thể chèn code gọi IEmailService ở đây sau này.

            return Ok(new { success = true, message = "Cập nhật trạng thái thành công" });
        }

        [HttpGet("my-jobs")]
        public async Task<IActionResult> GetMyJobs()
        {
            int maUser = GetCurrentUserId();

            // Tìm mã công ty của user đang đăng nhập
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
            if (company == null) return BadRequest(new { message = "Vui lòng cập nhật hồ sơ công ty trước khi xem tin đăng." });

            // Lấy danh sách chi tiết vị trí (kèm chiến dịch Master) của công ty này
            var myJobs = await _context.ChiTietViTris // Tên bảng có thể thay đổi tùy DB của bạn
                .Include(v => v.MaTinNavigation)
                .Where(v => v.MaTinNavigation.MaCongTy == company.MaCongTy)
                .Select(v => new
                {
                    maViTri = v.MaViTri,
                    tieuDe = v.MaTinNavigation.TieuDeChienDich + " - " + v.TenViTri, // Ghép Master và Detail
                    ngayTao = v.MaTinNavigation.NgayHetHan,
                    trangThai = v.MaTinNavigation.TrangThai, // 0: Chờ duyệt, 1: Đã duyệt
                    soLuongUngVien = _context.DonUngTuyens.Count(d => d.MaViTri == v.MaViTri) // Đếm số đơn nộp
                })
                .OrderByDescending(v => v.ngayTao)
                .ToListAsync();

            return Ok(myJobs);
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