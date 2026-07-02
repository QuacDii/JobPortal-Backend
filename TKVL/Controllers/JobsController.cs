using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class JobsController : ControllerBase
    {
        private readonly JobPortalDbContext _context; 

        public JobsController(JobPortalDbContext context)
        {
            _context = context;
        }

        // =================================================================
        // API 1: GET /api/jobs (Lấy toàn bộ vị trí việc làm hiển thị lên Trang Chủ)
        // =================================================================
        [HttpGet]
        public async Task<IActionResult> GetHomeJobs()
        {
            try
            {
                // Đứng từ bảng Cha (TinTuyenDung) để kéo các vị trí con (ChiTietViTris) về
                var campaigns = await _context.TinTuyenDungs
                    .Include(t => t.MaCongTyNavigation)
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(c => c.MaPhuongNavigation)
                            .ThenInclude(p => p.MaTpNavigation)
                    .Where(t => t.TrangThai == 1) // Chỉ lấy chiến dịch đã duyệt
                    .OrderByDescending(t => t.MaTin)
                    .Select(t => new
                    {
                        maTin = t.MaTin,
                        tieuDeChienDich = t.TieuDeChienDich,
                        companyName = t.MaCongTyNavigation!.TenCongTy,
                        logo = t.MaCongTyNavigation!.Logo,
                        deadline = t.NgayHetHan,
                        // Gom toàn bộ các vị trí con thuộc chiến dịch này vào một mảng
                        viTris = t.ChiTietViTris.Select(c => new {
                            id = c.MaViTri,
                            title = c.TenViTri,
                            salaryRange = c.Luong,
                            locationName = c.MaPhuongNavigation!.MaTpNavigation!.TenTp
                        }).ToList()
                    })
                    .ToListAsync();

                return Ok(new { success = true, data = campaigns });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
        // =================================================================
        // API 2: GET /api/jobs/{id} (Lấy thông tin CHI TIẾT của 1 công việc cụ thể)
        // =================================================================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetJobDetail(int id)
        {
            try
            {
                var jobDetail = await _context.ChiTietViTris
                    .Include(c => c.MaTinNavigation)
                        .ThenInclude(t => t.MaCongTyNavigation)
                    .Include(c => c.MaPhuongNavigation)
                        .ThenInclude(p => p.MaTpNavigation)
                    .FirstOrDefaultAsync(c => c.MaViTri == id);

                if (jobDetail == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy công việc này trong hệ thống!" });
                }

                // Trả ra toàn bộ ruột gan thông tin để làm trang chi tiết tuyển dụng
                return Ok(new
                {
                    success = true,
                    message = "Tải chi tiết công việc thành công!",
                    data = new
                    {
                        id = jobDetail.MaViTri,
                        title = jobDetail.TenViTri,
                        salaryRange = jobDetail.Luong,
                        soLuong = jobDetail.SoLuongTuyen,
                        description = jobDetail.MoTaCongViec,
                        requirements = jobDetail.YeuCauUngVien,
                        benefits = jobDetail.QuyenLoi,
                        companyName = jobDetail.MaTinNavigation!.MaCongTyNavigation!.TenCongTy,
                        companyDescription = jobDetail.MaTinNavigation!.MaCongTyNavigation!.MoTa,
                        logo = jobDetail.MaTinNavigation!.MaCongTyNavigation!.Logo,
                        address = jobDetail.MaTinNavigation!.MaCongTyNavigation!.DiaChi,
                        locationName = jobDetail.MaPhuongNavigation!.MaTpNavigation!.TenTp,
                        phuongXa = jobDetail.MaPhuongNavigation!.TenPhuong,
                        deadline = jobDetail.MaTinNavigation!.NgayHetHan,
                        nganhNgheKhac = jobDetail.NganhNgheKhac
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Đã xảy ra lỗi hệ thống khi tải chi tiết việc làm!",
                    error = ex.Message
                });
            }
        }
    }
}