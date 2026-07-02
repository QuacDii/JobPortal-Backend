using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TKVL.Models;
using TKVL.DTOs;
using System.Text.Json;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RecruitmentController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public RecruitmentController(JobPortalDbContext context)
        {
            _context = context;
        }

        // 1. API TRẢ VỀ DANH SÁCH GỢI Ý KỸ NĂNG CHO FRONTEND
        [HttpGet("skills")]
        [AllowAnonymous] // Cho phép ai cũng gọi được để gợi ý (không cần token cũng được)
        public async Task<IActionResult> GetStandardSkills()
        {
            // Chỉ bốc những kỹ năng đã được duyệt (TrangThai = true)
            var skills = await _context.KyNangs
                .Where(k => k.TrangThai == true)
                .Select(k => new {
                    value = k.TenKyNang,
                    label = k.TenKyNang
                }) // Format chuẩn cho component <Select> của Ant Design
                .ToListAsync();

            return Ok(skills);
        }

        // API LẤY DANH SÁCH NGÀNH NGHỀ
        [HttpGet("industries")]
        [AllowAnonymous]
        public async Task<IActionResult> GetIndustries()
        {
            var industries = await _context.NganhNghes
                .Where(n => n.TrangThai == true) // Chỉ lấy ngành nghề đang Active
                .Select(n => new {
                    value = n.MaNganh,
                    label = n.TenNganh
                })
                .ToListAsync();

            return Ok(industries);
        }

        // API LẤY DANH SÁCH TỈNH THÀNH & PHƯỜNG XÃ (Dạng Cây - Tree)
        [HttpGet("locations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLocations()
        {
            // Load Tỉnh Thành kèm theo Phường Xã bên trong nó
            var locations = await _context.ThanhPhos
                .Include(t => t.PhuongXas)
                .Select(t => new {
                    value = "TP_" + t.MaTp, // Thêm tiền tố để ID tỉnh không bị trùng với ID phường
                    label = t.TenTp,
                    // Danh sách Phường/Xã con
                    children = t.PhuongXas.Select(p => new {
                        value = p.MaPhuong,
                        label = p.TenPhuong
                    })
                })
                .ToListAsync();

            return Ok(locations);
        }

        // 2. API ĐĂNG TIN CHIẾN DỊCH (MASTER - DETAIL)
        [HttpPost("post-job")]
        public async Task<IActionResult> PostJobCampaign([FromBody] PostJobRequestDto request)
        {
            int maUser = GetCurrentUserId();
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);

            // Validate sơ bộ
            if (company == null)
                return BadRequest(new { success = false, message = "Bạn chưa khởi tạo Hồ sơ doanh nghiệp!" });

            if (company.TrangThai == false)
                return BadRequest(new { success = false, message = "Hồ sơ của bạn đang chờ duyệt. Không thể đăng tin lúc này." });

            if (request.DanhSachViTri == null || request.DanhSachViTri.Count == 0)
                return BadRequest(new { success = false, message = "Vui lòng thêm ít nhất 1 vị trí công việc!" });

            // KHỞI TẠO GIAO DỊCH (TRANSACTION)
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // BƯỚC 1: Lưu Chiến dịch (Master)
                var newCampaign = new TinTuyenDung
                {
                    MaCongTy = company.MaCongTy,
                    TieuDeChienDich = request.TieuDeChienDich,
                    NgayHetHan = request.NgayHetHan,
                    TrangThai = 0, // Tin vừa đăng phải đưa vào trạng thái Chờ Admin duyệt
                    IsPromoted = false
                };

                _context.TinTuyenDungs.Add(newCampaign);
                await _context.SaveChangesAsync(); // Cần SaveChanges để EF Core sinh ra Mã Tin mới

                // BƯỚC 2: Lưu các Vị trí (Detail)
                foreach (var posDto in request.DanhSachViTri)
                {
                    var newPosition = new ChiTietViTri
                    {
                        MaTin = newCampaign.MaTin, // Nối khóa ngoại về Master
                        TenViTri = posDto.TenViTri,
                        SoLuongTuyen = posDto.SoLuongTuyen,
                        Luong = posDto.Luong,
                        MoTaCongViec = posDto.MoTaCongViec,
                        YeuCauUngVien = posDto.YeuCauUngVien,
                        QuyenLoi = posDto.QuyenLoi,
                        MaNganh = posDto.MaNganh,
                        MaPhuong = posDto.MaPhuong,
                        NganhNgheKhac = posDto.NganhNgheKhac
                    };

                    _context.ChiTietViTris.Add(newPosition);
                    await _context.SaveChangesAsync();

                    // BƯỚC 3: Xử lý Kỹ năng (Folksonomy - Gắn tag động)
                    if (posDto.DanhSachKyNang != null && posDto.DanhSachKyNang.Any())
                    {
                        var kyNangEntities = new List<KyNang>();

                        foreach (var tenKN in posDto.DanhSachKyNang)
                        {
                            var keyword = tenKN.Trim();
                            if (string.IsNullOrEmpty(keyword)) continue;

                            // Tìm trong DB xem kỹ năng này có chưa (Bất chấp hoa thường nhờ ToLower)
                            var existingSkill = await _context.KyNangs
                                .FirstOrDefaultAsync(k => k.TenKyNang.ToLower() == keyword.ToLower());

                            if (existingSkill != null)
                            {
                                kyNangEntities.Add(existingSkill);
                            }
                            else
                            {
                                // Từ khóa hoàn toàn mới -> Lưu nháp (TrangThai = false)
                                var newSkill = new KyNang
                                {
                                    TenKyNang = keyword,
                                    TrangThai = false
                                };
                                _context.KyNangs.Add(newSkill);
                                await _context.SaveChangesAsync();

                                kyNangEntities.Add(newSkill);
                            }
                        }

                        // Nhét List Kỹ năng vào Vị trí, EF Core tự động INSERT vào bảng trung gian ViTri_KyNang
                        newPosition.MaKyNangs = kyNangEntities;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync(); // Xác nhận Transaction thành công!

                return Ok(new { success = true, message = "Đã gửi chiến dịch thành công! Vui lòng chờ Ban quản trị duyệt tin." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(); // Nếu lỗi bất kỳ khâu nào, hủy mọi thay đổi trong DB
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi lưu chiến dịch." });
            }
        }

        private int GetCurrentUserId()
        {
            var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                     ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                     ?? User.Claims.FirstOrDefault(c => c.Type == "sub");
            return int.Parse(claim.Value);
        }
    }
}