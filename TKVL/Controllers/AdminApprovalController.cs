using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TKVL.DTOs;
using TKVL.DTOs.Company;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "0")] // Chỉ Admin có quyền truy cập
    public class AdminApprovalController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IEmailService _emailService;

        public AdminApprovalController(
            JobPortalDbContext context,
            IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // =================================================================
        // LUỒNG 1: THẨM ĐỊNH & KIỂM DUYỆT HỒ SƠ DOANH NGHIỆP
        // =================================================================

        [HttpGet("pending-companies")]
        public async Task<IActionResult> GetPendingCompanies()
        {
            try
            {
                var rawCompanies = await _context.CongTies
                    .Include(c => c.MaUserNavigation)
                    .Where(c => c.TrangThai == false || (c.TrangThai == true && c.DuLieuChoDuyetJson != null))
                    .ToListAsync();

                var result = rawCompanies.Select(c => {
                    CompanyPendingUpdateDto pendingData = null;
                    if (!string.IsNullOrEmpty(c.DuLieuChoDuyetJson))
                    {
                        try { pendingData = JsonSerializer.Deserialize<CompanyPendingUpdateDto>(c.DuLieuChoDuyetJson); } catch { }
                    }

                    return new PendingCompanyListDto
                    {
                        MaCongTy = c.MaCongTy,
                        TenCongTy = c.TenCongTy,
                        MaSoThue = c.MaSoThue,
                        DiaChi = c.DiaChi,
                        QuyMo = c.QuyMo,
                        NguoiDaiDien = c.MaUserNavigation?.HoTen,
                        Email = c.MaUserNavigation?.Email,
                        TrangThaiHienTai = c.TrangThai,
                        LoaiYeuCau = (c.TrangThai == true && pendingData != null) ? "UPDATE" : "NEW",
                        ThongTinChoDuyet = pendingData
                    };
                }).OrderByDescending(x => x.LoaiYeuCau == "NEW").ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet("company-detail/{id}")]
        public async Task<IActionResult> GetCompanyDetail(int id)
        {
            try
            {
                var company = await _context.CongTies
                    .Include(c => c.MaUserNavigation)
                    .FirstOrDefaultAsync(c => c.MaCongTy == id);

                if (company == null)
                    return NotFound(new { success = false, message = "Không tìm thấy thông tin công ty!" });

                CompanyPendingUpdateDto pendingData = null;
                if (!string.IsNullOrEmpty(company.DuLieuChoDuyetJson))
                {
                    try { pendingData = JsonSerializer.Deserialize<CompanyPendingUpdateDto>(company.DuLieuChoDuyetJson); } catch { }
                }

                return Ok(new
                {
                    company.MaCongTy,
                    company.TenCongTy,
                    company.MaSoThue,
                    company.DiaChi,
                    company.QuyMo,
                    company.MoTa,
                    company.Logo,
                    company.GiayPhepKinhDoanhMatTruoc,
                    company.GiayPhepKinhDoanhMatSau,
                    company.YeuCauBoSung,
                    company.TrangThai,
                    company.MauEmailInterview,
                    NguoiDaiDien = company.MaUserNavigation?.HoTen,
                    Email = company.MaUserNavigation?.Email,
                    LoaiYeuCau = (company.TrangThai == true && pendingData != null) ? "UPDATE" : "NEW",
                    ThongTinChoDuyet = pendingData
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("review-company/{id}")]
        public async Task<IActionResult> ReviewCompany(int id, [FromBody] ReviewCompanyDto request)
        {
            try
            {
                var company = await _context.CongTies
                    .Include(c => c.MaUserNavigation)
                    .FirstOrDefaultAsync(c => c.MaCongTy == id);

                if (company == null)
                    return NotFound(new { success = false, message = "Không tìm thấy công ty!" });

                string action = request.ActionType?.ToUpper();
                string employerEmail = company.MaUserNavigation?.Email;
                string employerName = company.MaUserNavigation?.HoTen ?? "Nhà tuyển dụng";

                if (action == "APPROVE" || request.IsApproved == true)
                {
                    if (!string.IsNullOrEmpty(company.DuLieuChoDuyetJson))
                    {
                        var pendingData = JsonSerializer.Deserialize<CompanyPendingUpdateDto>(company.DuLieuChoDuyetJson);
                        if (pendingData != null)
                        {
                            company.TenCongTy = pendingData.TenCongTy;
                            company.MaSoThue = pendingData.MaSoThue;
                            company.GiayPhepKinhDoanhMatTruoc = pendingData.GiayPhepKinhDoanhMatTruoc;
                            company.GiayPhepKinhDoanhMatSau = pendingData.GiayPhepKinhDoanhMatSau;
                        }
                        company.DuLieuChoDuyetJson = null;
                    }

                    company.TrangThai = true;
                    company.YeuCauBoSung = null;
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(employerEmail))
                    {
                        string emailBody = $@"
                            <p>Chào <b>{employerName}</b>,</p>
                            <p>Yêu cầu thay đổi/xác minh thông tin doanh nghiệp <b>{company.TenCongTy}</b> đã được <b>Phê duyệt chính thức</b>.</p>
                            <br/><p>Trân trọng,<br/><b>Ban quản trị JobsNow System</b></p>";
                        _ = _emailService.SendEmailAsync(employerEmail, $"[JobsNow] Hồ sơ doanh nghiệp {company.TenCongTy} đã được phê duyệt", emailBody);
                    }
                }
                else if (action == "REQUEST_ADDITION")
                {
                    company.YeuCauBoSung = request.YeuCauBoSung;
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(employerEmail))
                    {
                        string emailBody = $@"
                            <p>Chào <b>{employerName}</b>,</p>
                            <p>Yêu cầu cập nhật hồ sơ doanh nghiệp <b>{company.TenCongTy}</b> cần được <b>bổ sung/chỉnh sửa</b>:</p>
                            <div style='background-color: #fef2f2; border-left: 4px solid #ef4444; padding: 12px; margin: 15px 0;'>
                                <b>Yêu cầu từ Admin:</b> <i>{request.YeuCauBoSung}</i>
                            </div>
                            <br/><p>Trân trọng,<br/><b>Ban quản trị JobsNow System</b></p>";
                        _ = _emailService.SendEmailAsync(employerEmail, $"[JobsNow] Yêu cầu bổ sung hồ sơ doanh nghiệp {company.TenCongTy}", emailBody);
                    }
                }
                else if (action == "REJECT")
                {
                    if (company.TrangThai == true && !string.IsNullOrEmpty(company.DuLieuChoDuyetJson))
                    {
                        company.DuLieuChoDuyetJson = null;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        _context.CongTies.Remove(company);
                        await _context.SaveChangesAsync();
                    }

                    if (!string.IsNullOrEmpty(employerEmail))
                    {
                        string emailBody = $@"
                            <p>Chào <b>{employerName}</b>,</p>
                            <p>Yêu cầu thay đổi thông tin doanh nghiệp <b>{company.TenCongTy}</b> đã bị <b>Từ chối</b> do không đạt yêu cầu thẩm định.</p>
                            <br/><p>Trân trọng,<br/><b>Ban quản trị JobsNow System</b></p>";
                        _ = _emailService.SendEmailAsync(employerEmail, $"[JobsNow] Thông báo từ chối cập nhật hồ sơ {company.TenCongTy}", emailBody);
                    }
                }

                return Ok(new { success = true, message = "Cập nhật kết quả thẩm định thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // =================================================================
        // LUỒNG 2: KIỂM DUYỆT TIN TUYỂN DỤNG
        // =================================================================

        // =================================================================
        // 1. GET: LẤY DANH SÁCH CHIẾN DỊCH CÒN VỊ TRÍ CHỜ DUYỆT
        // =================================================================
        [HttpGet("pending-job-posts")]
        public async Task<IActionResult> GetPendingJobPosts()
        {
            try
            {
                // 🌟 SỬA ĐIỀU KIỆN: Chỉ cần chiến dịch chờ duyệt HOẶC còn vị trí con đang chờ (TrangThai == 0)
                var pendingJobs = await _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation)
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(v => v.MaNganhConNavigation)
                    .Where(t => t.TrangThai == 0 || t.ChiTietViTris.Any(v => v.TrangThai == 0))
                    .Select(t => new
                    {
                        t.MaTin,
                        TieuDeChienDich = t.TieuDeChienDich,
                        TenCongTy = t.MaCongTyNavigation.TenCongTy,
                        LogoCongTy = t.MaCongTyNavigation.Logo,
                        t.NgayHetHan,
                        t.IsPromoted,
                        DanhSachViTri = t.ChiTietViTris.Select(v => new
                        {
                            v.MaViTri,
                            v.TenViTri,
                            v.Luong,
                            TenNganh = v.MaNganhConNavigation != null ? v.MaNganhConNavigation.TenNganhCon : null,
                            v.SoLuongTuyen,
                            v.MoTaCongViec,
                            v.YeuCauUngVien,
                            v.QuyenLoi,
                            v.TrangThai,
                            v.LyDoTuChoi,
                            v.NgayHetHan
                        }).ToList()
                    })
                    .OrderBy(t => t.NgayHetHan)
                    .ToListAsync();

                return Ok(pendingJobs);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // =================================================================
        // 2. PUT: DUYỆT TOÀN BỘ CÁC VỊ TRÍ TRONG CHIẾN DỊCH
        // =================================================================
        [HttpPut("approve-all-positions/{maTin}")]
        public async Task<IActionResult> ApproveAllPositions(int maTin)
        {
            try
            {
                var campaign = await _context.TinTuyenDungs
                    .Include(t => t.ChiTietViTris)
                    .Include(t => t.MaCongTyNavigation)
                        .ThenInclude(c => c.MaUserNavigation)
                    .FirstOrDefaultAsync(t => t.MaTin == maTin);

                if (campaign == null)
                    return NotFound(new { success = false, message = "Không tìm thấy chiến dịch tuyển dụng!" });

                // Cập nhật tất cả các vị trí đang chờ (0) thành đã duyệt (1)
                foreach (var pos in campaign.ChiTietViTris)
                {
                    if (pos.TrangThai == 0)
                    {
                        pos.TrangThai = 1;
                        pos.LyDoTuChoi = null;
                    }
                }

                campaign.TrangThai = 1; // Chiến dịch chính thức lên sóng
                await _context.SaveChangesAsync();

                // Gửi email thông báo cho NTD
                string? employerEmail = campaign.MaCongTyNavigation?.MaUserNavigation?.Email;
                string employerName = campaign.MaCongTyNavigation?.MaUserNavigation?.HoTen ?? "Nhà tuyển dụng";
                if (!string.IsNullOrEmpty(employerEmail))
                {
                    string emailBody = $@"
                <p>Chào <b>{employerName}</b>,</p>
                <p>Toàn bộ các vị trí trong chiến dịch tuyển dụng <b>'{campaign.TieuDeChienDich}'</b> của bạn đã được <b>Phê duyệt thành công</b> và đang mở nhận hồ sơ trên hệ thống.</p>
                <br/><p>Trân trọng,<br/><b>Ban quản trị JobsNow System</b></p>";

                    _ = _emailService.SendEmailAsync(employerEmail, $"[JobsNow] Toàn bộ chiến dịch '{campaign.TieuDeChienDich}' đã được phê duyệt", emailBody);
                }

                return Ok(new { success = true, message = "Đã duyệt toàn bộ các vị trí trong chiến dịch!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("review-job-post/{id}")]
        public async Task<IActionResult> ReviewJobPost(int id, [FromBody] ReviewJobPostDto request)
        {
            try
            {
                var jobPost = await _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation)
                        .ThenInclude(c => c.MaUserNavigation)
                    .FirstOrDefaultAsync(t => t.MaTin == id);

                if (jobPost == null)
                    return NotFound(new { success = false, message = "Không tìm thấy tin tuyển dụng!" });

                string employerEmail = jobPost.MaCongTyNavigation?.MaUserNavigation?.Email;
                string employerName = jobPost.MaCongTyNavigation?.MaUserNavigation?.HoTen ?? "Nhà tuyển dụng";

                if (request.IsApproved)
                {
                    jobPost.TrangThai = 1;
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(employerEmail))
                    {
                        string emailBody = $@"
                            <p>Chào <b>{employerName}</b>,</p>

                            <p>Tin tuyển dụng <b>'{jobPost.TieuDeChienDich}'</b> của bạn đã được Admin phê duyệt và chính thức hiển thị công khai trên cổng việc làm JobsNow.</p>
                            <br/><p>Trân trọng,<br/><b>Ban quản trị JobsNow System</b></p>";
                        _ = _emailService.SendEmailAsync(employerEmail, $"[JobsNow] Tin tuyển dụng '{jobPost.TieuDeChienDich}' đã được phê duyệt", emailBody);
                    }
                }
                else
                {
                    jobPost.TrangThai = 2;
                    await _context.SaveChangesAsync();

                    if (!string.IsNullOrEmpty(employerEmail))
                    {
                        string emailBody = $@"
                            <p>Chào <b>{employerName}</b>,</p>

                            <p>Tin tuyển dụng <b>'{jobPost.TieuDeChienDich}'</b> của bạn chưa đạt yêu cầu đăng tin và đã bị <b>Từ chối</b>.</p>

                            <p>Vui lòng kiểm tra lại nội dung chi tiết vị trí tuyển dụng hoặc liên hệ Admin để được hỗ trợ.</p>
                            <br/><p>Trân trọng,<br/><b>Ban quản trị JobsNow System</b></p>";
                        _ = _emailService.SendEmailAsync(employerEmail, $"[JobsNow] Tin tuyển dụng '{jobPost.TieuDeChienDich}' đã bị từ chối", emailBody);
                    }
                }

                return Ok(new
                {
                    success = true,
                    message = request.IsApproved ? "Đã phê duyệt tin tuyển dụng thành công!" : "Đã từ chối tin tuyển dụng!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpPut("review-position")]
        public async Task<IActionResult> ReviewPosition([FromBody] ReviewPositionDto request)
        {
            try
            {
                var position = await _context.ChiTietViTris
                    .Include(v => v.MaTinNavigation)
                        .ThenInclude(t => t.MaCongTyNavigation)
                            .ThenInclude(c => c.MaUserNavigation)
                    .FirstOrDefaultAsync(v => v.MaViTri == request.MaViTri);

                if (position == null)
                    return NotFound(new { success = false, message = "Không tìm thấy vị trí tuyển dụng!" });

                position.TrangThai = (byte)(request.IsApproved ? 1 : 3); // 1: Đang mở, 3: Bị từ chối
                position.LyDoTuChoi = request.IsApproved ? null : request.LyDoTuChoi;

                // Tự động kiểm tra trạng thái toàn chiến dịch
                var allPositions = await _context.ChiTietViTris
                    .Where(v => v.MaTin == position.MaTin)
                    .ToListAsync();

                var campaign = position.MaTinNavigation;
                if (allPositions.Any(v => v.TrangThai == 1))
                {
                    campaign.TrangThai = 1; // Chiến dịch được lên sóng nếu có ít nhất 1 vị trí được duyệt
                }
                else if (allPositions.All(v => v.TrangThai == 3))
                {
                    campaign.TrangThai = 2; // Tạm dừng/từ chối toàn bộ nếu tất cả vị trí đều bị từ chối
                }

                await _context.SaveChangesAsync();

                // Gửi email thông báo cho Nhà tuyển dụng
                string? employerEmail = campaign.MaCongTyNavigation?.MaUserNavigation?.Email;
                string employerName = campaign.MaCongTyNavigation?.MaUserNavigation?.HoTen ?? "Nhà tuyển dụng";
                if (!string.IsNullOrEmpty(employerEmail))
                {
                    string emailBody = request.IsApproved
                        ? $"<p>Chào <b>{employerName}</b>,</p><p>Vị trí <b>'{position.TenViTri}'</b> trong chiến dịch <b>'{campaign.TieuDeChienDich}'</b> đã được <b>Phê duyệt</b> và đang mở nhận hồ sơ.</p>"
                        : $"<p>Chào <b>{employerName}</b>,</p><p>Vị trí <b>'{position.TenViTri}'</b> trong chiến dịch <b>'{campaign.TieuDeChienDich}'</b> đã bị <b>Từ chối</b>.<br/><b>Lý do:</b> {request.LyDoTuChoi}</p>";

                    _ = _emailService.SendEmailAsync(employerEmail, $"[JobsNow] Kết quả kiểm duyệt vị trí {position.TenViTri}", emailBody);
                }

                return Ok(new { success = true, message = request.IsApproved ? "Đã duyệt vị trí thành công!" : "Đã từ chối vị trí!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
}