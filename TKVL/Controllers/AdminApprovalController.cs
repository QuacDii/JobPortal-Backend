using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "0")]
    public class AdminApprovalController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public AdminApprovalController(JobPortalDbContext context)
        {
            _context = context;
        }

        // 1. Lấy danh sách doanh nghiệp đang chờ duyệt (trangThai == false)
        [HttpGet("pending-companies")]
        public async Task<IActionResult> GetPendingCompanies()
        {
            try
            {
                var companies = await _context.CongTies
                    .Where(c => c.TrangThai == false)
                    .Select(c => new
                    {
                        c.MaCongTy,
                        c.TenCongTy,
                        c.MaSoThue,
                        c.DiaChi,
                        c.QuyMo,
                        NguoiDaiDien = c.MaUserNavigation.HoTen, // Lấy tên user tạo công ty
                        Email = c.MaUserNavigation.Email
                    })
                    .ToListAsync();

                return Ok(companies);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // 2. API Phê duyệt hoặc Từ chối doanh nghiệp
        [HttpPut("review-company/{id}")]
        public async Task<IActionResult> ReviewCompany(int id, [FromBody] ReviewRequestDto request)
        {
            try
            {
                var company = await _context.CongTies.FindAsync(id);
                if (company == null) return NotFound(new { message = "Không tìm thấy công ty!" });

                if (request.IsApproved)
                {
                    company.TrangThai = true; // Phê duyệt
                }
                else
                {
                    _context.CongTies.Remove(company);
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = request.IsApproved ? "Đã phê duyệt công ty!" : "Đã từ chối công ty!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // =================================================================
        // 3. Lấy danh sách Chiến dịch đang chờ duyệt (trangThai == 0)
        // =================================================================
        [HttpGet("pending-campaigns")]
        public async Task<IActionResult> GetPendingCampaigns()
        {
            try
            {
                var campaigns = await _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation) // Join với bảng Công Ty để lấy tên
                    .Where(t => t.TrangThai == 0) // Chỉ lấy các tin chờ duyệt
                    .Select(t => new
                    {
                        t.MaTin,
                        t.TieuDeChienDich,
                        TenCongTy = t.MaCongTyNavigation.TenCongTy,
                        t.NgayHetHan,
                        t.IsPromoted
                    })
                    .OrderBy(t => t.NgayHetHan) // Ưu tiên xử lý các tin sắp hết hạn
                    .ToListAsync();

                return Ok(campaigns);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // =================================================================
        // 4. API Phê duyệt hoặc Từ chối Chiến dịch
        // =================================================================
        [HttpPut("review-campaign/{id}")]
        public async Task<IActionResult> ReviewCampaign(int id, [FromBody] ReviewRequestDto request)
        {
            try
            {
                var campaign = await _context.TinTuyenDungs.FindAsync(id);
                if (campaign == null) return NotFound(new { message = "Không tìm thấy chiến dịch!" });

                if (request.IsApproved)
                {
                    campaign.TrangThai = 1; // 1: Đã duyệt -> Cho phép hiển thị lên trang chủ
                }
                else
                {
                    campaign.TrangThai = 2; // 2: Từ chối hiển thị
                }

                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = request.IsApproved ? "Đã phê duyệt chiến dịch!" : "Đã từ chối chiến dịch!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }
    }
    public class ReviewRequestDto
    {
        public bool IsApproved { get; set; }
    }
}
