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
                // Lấy cả 2 nhóm:
                // 1. Hồ sơ đăng ký mới (TrangThai == false)
                // 2. Hồ sơ đang chạy có yêu cầu cập nhật (DuLieuChoDuyetJson != null)
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
                }).OrderByDescending(x => x.LoaiYeuCau == "NEW").ToList(); // Ưu tiên xếp hồ sơ mới lên đầu

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
                    ThongTinChoDuyet = pendingData // Nếu có dữ liệu này, Frontend sẽ bật chế độ SO SÁNH (Diff)
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
                    // 1. PHÊ DUYỆT
                    if (!string.IsNullOrEmpty(company.DuLieuChoDuyetJson))
                    {
                        // Kịch bản A: Duyệt yêu cầu CẬP NHẬT -> Ghi đè bản nháp vào dữ liệu chính
                        var pendingData = JsonSerializer.Deserialize<CompanyPendingUpdateDto>(company.DuLieuChoDuyetJson);
                        if (pendingData != null)
                        {
                            company.TenCongTy = pendingData.TenCongTy;
                            company.MaSoThue = pendingData.MaSoThue;
                            company.GiayPhepKinhDoanhMatTruoc = pendingData.GiayPhepKinhDoanhMatTruoc;
                            company.GiayPhepKinhDoanhMatSau = pendingData.GiayPhepKinhDoanhMatSau;
                        }
                        company.DuLieuChoDuyetJson = null; // Xóa bản nháp sau khi đè thành công
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
                    // 2. YÊU CẦU BỔ SUNG
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
                    // 3. TỪ CHỐI
                    if (company.TrangThai == true && !string.IsNullOrEmpty(company.DuLieuChoDuyetJson))
                    {
                        // Nếu là TỪ CHỐI BẢN CẬP NHẬT -> Chỉ xóa bản nháp, KHÔNG xóa công ty, giữ nguyên thông tin cũ đang chạy
                        company.DuLieuChoDuyetJson = null;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        // Nếu là TỪ CHỐI HỒ SƠ MỚI -> Xóa công ty khỏi hệ thống
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

        [HttpGet("pending-job-posts")]
        public async Task<IActionResult> GetPendingJobPosts()
        {
            try
            {
                var pendingJobs = await _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation)
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(v => v.MaNganhNavigation)
                    .Where(t => t.TrangThai == 0)
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
                            TenNganh = v.MaNganhNavigation.TenNganh,
                            v.SoLuongTuyen,
                            v.MoTaCongViec,
                            v.YeuCauUngVien,
                            v.QuyenLoi
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
    }
}