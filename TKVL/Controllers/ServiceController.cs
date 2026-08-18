using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TKVL.Models;
using TKVL.Services;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
        private readonly JobPortalDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IServiceProvider _serviceProvider;

        public ServiceController(JobPortalDbContext context, IEmailService emailService, IServiceProvider serviceProvider)
        {
            _context = context;
            _emailService = emailService;
            _serviceProvider = serviceProvider;
        }

        // =================================================================
        // PHẦN 1: CÁC API DÀNH CHO ADMIN (QUẢN LÝ CRUD GÓI DỊCH VỤ)
        // =================================================================

        [HttpGet]
        public async Task<IActionResult> GetAllForAdmin()
        {
            var packages = await _context.GoiDichVus.OrderByDescending(g => g.MaGoi).ToListAsync();
            return Ok(packages);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePackage([FromBody] GoiDichVu package)
        {
            package.TrangThai = true;
            _context.GoiDichVus.Add(package);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Thêm gói dịch vụ thành công!", package });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var package = await _context.GoiDichVus.FindAsync(id);
            if (package == null) return NotFound(new { message = "Không tìm thấy gói!" });

            bool hasTransactions = await _context.GiaoDiches.AnyAsync(g => g.MaGoi == id);

            if (!hasTransactions)
            {
                _context.GoiDichVus.Remove(package);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã xóa vĩnh viễn gói dịch vụ này vì chưa có giao dịch!" });
            }
            else
            {
                package.TrangThai = false;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã ngưng bán gói dịch vụ này (Dữ liệu vẫn được giữ cho báo cáo)!" });
            }
        }

        [HttpPut("{id}/restore")]
        public async Task<IActionResult> RestorePackage(int id)
        {
            var package = await _context.GoiDichVus.FindAsync(id);
            if (package == null) return NotFound(new { message = "Không tìm thấy gói!" });

            package.TrangThai = true;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Đã mở bán lại gói dịch vụ này!" });
        }


        // =================================================================
        // PHẦN 2: CÁC API DÀNH CHO NGƯỜI DÙNG / NHÀ TUYỂN DỤNG
        // =================================================================

        [HttpGet("admin/packages")]
        public async Task<IActionResult> GetAllPackagesForAdmin()
        {
            var packages = await _context.GoiDichVus
                .Select(g => new
                {
                    g.MaGoi,
                    g.TenGoi,
                    g.LoaiGoi,
                    g.GiaTien,
                    g.GiaKhuyenMai,
                    g.DonViThoiGian,
                    g.DoiTuongSuDung,
                    g.TrangThai,
                    DacQuyens = g.GoiDichVu_DacQuyens.Select(dq => new
                    {
                        dq.DacQuyen.MaDacQuyen,
                        dq.DacQuyen.MaCode,
                        dq.DacQuyen.TenDacQuyen,
                        dq.SoLuong
                    })
                })
                .ToListAsync();

            return Ok(packages);
        }

        [HttpGet("employer-packages")]
        public async Task<IActionResult> GetEmployerPackages()
        {
            var packages = await GetActivePackagesByTargetAsync(1);
            return Ok(packages);
        }

        [HttpGet("candidate-packages")]
        public async Task<IActionResult> GetCandidatePackages()
        {
            var packages = await GetActivePackagesByTargetAsync(2);
            return Ok(packages);
        }

        private async Task<object> GetActivePackagesByTargetAsync(byte doiTuong)
        {
            return await _context.GoiDichVus
                .Where(g => g.TrangThai == true && g.DoiTuongSuDung == doiTuong)
                .Select(g => new
                {
                    g.MaGoi,
                    g.TenGoi,
                    g.LoaiGoi,
                    g.GiaTien,
                    g.GiaKhuyenMai,
                    g.DonViThoiGian,
                    g.DoiTuongSuDung,
                    DacQuyens = g.GoiDichVu_DacQuyens.Select(dq => new
                    {
                        dq.DacQuyen.MaDacQuyen,
                        dq.DacQuyen.MaCode,
                        dq.DacQuyen.TenDacQuyen,
                        dq.SoLuong
                    })
                })
                .ToListAsync();
        }

        [HttpPost("purchase")]
        public async Task<IActionResult> PurchasePackage([FromBody] PurchaseRequestDto request)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (userIdClaim == null) return Unauthorized(new { message = "Vui lòng đăng nhập!" });
            int maUser = int.Parse(userIdClaim.Value);

            bool isJustPurchased = await _context.GiaoDiches.AnyAsync(g =>
                g.MaUser == maUser &&
                g.MaGoi == request.MaGoi &&
                g.LoaiGiaoDich == 2 &&
                g.TrangThai == true &&
                g.NgayGd >= DateTime.Now.AddSeconds(-60));

            if (isJustPurchased)
            {
                return BadRequest(new { message = "Gói dịch vụ này vừa được kích hoạt thành công. Vui lòng không thao tác lại!" });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);
                var package = await _context.GoiDichVus
                    .Include(g => g.GoiDichVu_DacQuyens)
                        .ThenInclude(gd => gd.DacQuyen)
                    .FirstOrDefaultAsync(g => g.MaGoi == request.MaGoi);

                if (user == null || package == null)
                    return BadRequest(new { message = "Dữ liệu không hợp lệ." });

                if (package.DoiTuongSuDung != user.VaiTro)
                    return BadRequest(new { message = "Gói dịch vụ này không dành cho vai trò tài khoản của bạn!" });

                decimal giaThucTe = (package.GiaKhuyenMai.HasValue && package.GiaKhuyenMai > 0)
                                    ? package.GiaKhuyenMai.Value
                                    : package.GiaTien;

                if (user.SoDuVi < giaThucTe)
                    return BadRequest(new { message = "Số dư ví không đủ. Vui lòng nạp thêm tiền!" });

                user.SoDuVi -= giaThucTe;

                var dacQuyenXemCv = package.GoiDichVu_DacQuyens
                    .FirstOrDefault(dq => dq.DacQuyen != null && dq.DacQuyen.MaCode == "NTD_UNLOCK_CV");

                if (dacQuyenXemCv != null && dacQuyenXemCv.SoLuong.HasValue)
                {
                    user.LuotXemCvConLai = (user.LuotXemCvConLai) + dacQuyenXemCv.SoLuong.Value;
                }

                DateTime ngayBatDau = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi > DateTime.Now
                                      ? user.NgayHetHanGoi.Value
                                      : DateTime.Now;

                switch (package.LoaiGoi)
                {
                    case 1: user.NgayHetHanGoi = ngayBatDau.AddDays(package.DonViThoiGian ?? 0); break;
                    case 2: user.NgayHetHanGoi = ngayBatDau.AddMonths(package.DonViThoiGian ?? 0); break;
                    case 3: user.NgayHetHanGoi = ngayBatDau.AddYears(package.DonViThoiGian ?? 0); break;
                    default: user.NgayHetHanGoi = ngayBatDau.AddDays(package.DonViThoiGian ?? 0); break;
                }

                foreach (var item in package.GoiDichVu_DacQuyens)
                {
                    var userDacQuyen = await _context.UserDacQuyens
                        .FirstOrDefaultAsync(ud => ud.MaUser == maUser && ud.MaDacQuyen == item.MaDacQuyen);

                    if (userDacQuyen != null)
                    {
                        if (item.SoLuong.HasValue)
                        {
                            userDacQuyen.SoLuotConLai = (userDacQuyen.SoLuotConLai ?? 0) + item.SoLuong.Value;
                        }
                        userDacQuyen.NgayHetHan = user.NgayHetHanGoi.Value;
                    }
                    else
                    {
                        _context.UserDacQuyens.Add(new UserDacQuyen
                        {
                            MaUser = maUser,
                            MaDacQuyen = item.MaDacQuyen,
                            SoLuotConLai = item.SoLuong,
                            NgayHetHan = user.NgayHetHanGoi.Value
                        });
                    }
                }

                var giaoDich = new GiaoDich
                {
                    MaUser = maUser,
                    MaGoi = package.MaGoi,
                    LoaiGiaoDich = 2,
                    SoTien = giaThucTe,
                    PhuongThuc = "Ví nội bộ",
                    NgayGd = DateTime.Now,
                    TrangThai = true
                };

                _context.GiaoDiches.Add(giaoDich);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                if (user.VaiTro == 1)
                {
                    var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
                    if (company != null)
                    {
                        _ = ProcessPendingAiAnalysesForEmployerAsync(company.MaCongTy, _serviceProvider);
                    }
                }

                return Ok(new
                {
                    message = "Đăng ký gói dịch vụ thành công!",
                    soDuMoi = user.SoDuVi,
                    ngayHetHanMoi = user.NgayHetHanGoi,
                    luotXemMoi = user.LuotXemCvConLai
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống khi xử lý giao dịch." });
            }
        }

        private async Task ProcessPendingAiAnalysesForEmployerAsync(int maCongTy, IServiceProvider serviceProvider)
        {
            _ = Task.Run(async () =>
            {
                using var scope = serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<JobPortalDbContext>();
                var aiService = scope.ServiceProvider.GetRequiredService<IAiAnalysisService>();

                try
                {
                    var unanalyzedAppIds = await context.DonUngTuyens
                        .Include(d => d.MaViTriNavigation)
                            .ThenInclude(v => v.MaTinNavigation)
                        .Where(d => d.MaViTriNavigation.MaTinNavigation.MaCongTy == maCongTy
                                 && d.MaViTriNavigation.MaTinNavigation.TrangThai == 1
                                 && (d.ChiTietPhanTichAi == null
                                  || d.ChiTietPhanTichAi.DiemMatchingTong == 0
                                  || d.ChiTietPhanTichAi.ThongTinHoSoTrichXuatJson == "{}"))
                        .Select(d => d.MaDon)
                        .ToListAsync();

                    foreach (var maDon in unanalyzedAppIds)
                    {
                        try
                        {
                            await aiService.AnalyzeApplicationAsync(maDon);
                            await Task.Delay(500);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[AI Retroactive Error] MaDon {maDon}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AI Retroactive General Error]: {ex.Message}");
                }
            });
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetTransactionHistory()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (userIdClaim == null) return Unauthorized();

            int maUser = int.Parse(userIdClaim.Value);

            var history = await _context.GiaoDiches
                .Where(g => g.MaUser == maUser)
                .OrderByDescending(g => g.NgayGd)
                .Select(g => new
                {
                    g.MaGd,
                    g.LoaiGiaoDich,
                    g.SoTien,
                    g.PhuongThuc,
                    g.NgayGd,
                    g.TrangThai,
                    TenGoi = g.MaGoiNavigation != null ? g.MaGoiNavigation.TenGoi : "Nạp tiền"
                })
                .ToListAsync();

            return Ok(history);
        }

        // =======================================================================
        // 🌟 BỔ SUNG LẤY DANH SÁCH MÃ ĐẶC QUYỀN ĐỂ KIỂM TRA QUYỀN VIP CV CHÍNH XÁC
        // =======================================================================
        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (userIdClaim == null) return Unauthorized(new { message = "Vui lòng đăng nhập" });

            int maUser = int.Parse(userIdClaim.Value);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUser);
            if (user == null) return NotFound();

            // 🌟 1. TÍNH TỔNG SỐ LƯỢT AI CÒN HẠN TỪ BẢNG UserDacQuyens
            var totalAiTurns = await _context.UserDacQuyens
                .Where(ud => ud.MaUser == maUser && ud.NgayHetHan > DateTime.Now)
                .Join(_context.DacQuyens,
                      ud => ud.MaDacQuyen,
                      dq => dq.MaDacQuyen,
                      (ud, dq) => new { ud.SoLuotConLai, dq.MaCode })
                .Where(x => x.MaCode == "UV_AI_REVIEW")
                .SumAsync(x => x.SoLuotConLai ?? 0);

            // 🌟 2. LẤY GIAO DỊCH MUA GÓI GẦN NHẤT CỦA USER
            bool isVipActive = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi > DateTime.Now;

            var lastPurchasedPackage = await _context.GiaoDiches
                .Include(g => g.MaGoiNavigation)
                .Where(g => g.MaUser == maUser && g.LoaiGiaoDich == 2 && g.TrangThai == true && g.MaGoi != null)
                .OrderByDescending(g => g.NgayGd)
                .FirstOrDefaultAsync();

            string tenGoi = "Miễn phí";
            DateTime? ngayMua = null;

            if (isVipActive && lastPurchasedPackage != null)
            {
                tenGoi = lastPurchasedPackage.MaGoiNavigation != null
                    ? lastPurchasedPackage.MaGoiNavigation.TenGoi
                    : "Tài khoản VIP";
                ngayMua = lastPurchasedPackage.NgayGd;
            }

            // 🌟 3. DANH SÁCH MÃ ĐẶC QUYỀN ĐANG CÓ HIỆU LỰC
            var activePrivileges = await _context.UserDacQuyens
                .Where(ud => ud.MaUser == maUser && ud.NgayHetHan > DateTime.Now)
                .Join(_context.DacQuyens,
                      ud => ud.MaDacQuyen,
                      dq => dq.MaDacQuyen,
                      (ud, dq) => dq.MaCode)
                .Distinct()
                .ToListAsync();

            // 🌟 4. TRẢ VỀ DỮ LIỆU RESPONSE ĐỒNG BỘ
            return Ok(new
            {
                soDuVi = user.SoDuVi,
                ngayHetHanGoi = user.NgayHetHanGoi,
                luotXemCvConLai = user.LuotXemCvConLai,
                soLuotAiConLai = totalAiTurns,
                tenGoiHienTai = tenGoi,
                ngayMua = ngayMua,
                cacDacQuyen = activePrivileges,
                danhSachGoiDaMua = isVipActive && lastPurchasedPackage != null
                    ? new List<object>
                    {
                new
                {
                    maGoi = lastPurchasedPackage.MaGoi,
                    tenGoi = lastPurchasedPackage.MaGoiNavigation != null ? lastPurchasedPackage.MaGoiNavigation.TenGoi : "Gói VIP",
                    soTien = lastPurchasedPackage.SoTien,
                    ngayMua = lastPurchasedPackage.NgayGd
                }
                    }
                    : new List<object>()
            });
        }
    }
    public class PurchaseRequestDto
    {
        public int MaGoi { get; set; }
    }
}