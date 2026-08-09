using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public CompanyController(JobPortalDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetCompanies([FromQuery] string? keyword = null)
        {
            var query = _context.CongTies.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(c => c.TenCongTy.Contains(keyword) || (c.MoTa != null && c.MoTa.Contains(keyword)));
            }

            var result = await query.Select(c => new
            {
                id = c.MaCongTy,
                tenCongTy = c.TenCongTy,
                logo = c.Logo,
                coverImage = "https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?q=80&w=1200&auto=format&fit=crop",
                moTa = c.MoTa,
                diaChi = c.DiaChi,
                quyMo = c.QuyMo,
                maSoThue = c.MaSoThue,
                soTinTuyenDung = _context.TinTuyenDungs.Count(t => t.MaCongTy == c.MaCongTy)
            }).ToListAsync();

            return Ok(new { success = true, data = result });
        }

        // 2. GET: api/Company/{id} (Chi tiết công ty)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCompanyDetail(int id)
        {
            try
            {
                var company = await _context.CongTies
                    .Where(c => c.MaCongTy == id)
                    .Select(c => new
                    {
                        id = c.MaCongTy,
                        tenCongTy = c.TenCongTy,
                        logo = c.Logo,
                        moTa = c.MoTa,
                        diaChi = c.DiaChi,
                        maSoThue = c.MaSoThue,
                        quyMo = c.QuyMo
                    })
                    .FirstOrDefaultAsync();

                if (company == null)
                    return NotFound(new { success = false, message = "Không tìm thấy công ty!" });

                var jobs = await _context.TinTuyenDungs
                    .Where(t => t.MaCongTy == id)
                    .Select(t => new
                    {
                        maTin = t.MaTin,
                        tieuDeChienDich = t.TieuDeChienDich,
                        companyName = company.tenCongTy,
                        logo = company.logo,
                        // 🌟 Trả về maViTri chính chủ từ bảng ChiTietViTri
                        maViTri = t.ChiTietViTris.Select(v => (int?)v.MaViTri).FirstOrDefault(),
                        viTris = t.ChiTietViTris.Select(v => new
                        {
                            id = v.MaViTri,
                            maViTri = v.MaViTri,
                            title = v.TenViTri,
                            luong = v.Luong,
                            locationName = _context.PhuongXas
                                .Where(p => p.MaPhuong == v.MaPhuong)
                                .Select(p => p.MaTpNavigation.TenTp)
                                .FirstOrDefault() ?? "Toàn quốc",
                            capBac = v.CapBac,
                            kinhNghiem = v.KinhNghiem
                        }).ToList()
                    }).ToListAsync();

                return Ok(new { success = true, company, jobs });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi máy chủ", error = ex.Message });
            }
        }
    }
}
