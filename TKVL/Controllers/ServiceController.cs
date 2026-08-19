using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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
        // PHẦN 1: CÁC API DÀNH CHO ADMIN
        // =================================================================

        [HttpGet]
        public async Task<IActionResult> GetAllForAdmin()
        {
            var packages = await _context.GoiDichVus.OrderByDescending(g => g.MaGoi).ToListAsync();
            return Ok(packages);
        }

        [HttpGet("privileges")]
        public async Task<IActionResult> GetAllPrivileges()
        {
            var privileges = await _context.DacQuyens
                .Select(d => new
                {
                    maDacQuyen = d.MaDacQuyen,
                    maCode = d.MaCode,
                    tenDacQuyen = d.TenDacQuyen,
                    doiTuongSuDung = d.DoiTuongSuDung,
                    moTa = d.MoTa
                })
                .ToListAsync();

            return Ok(privileges);
        }

        [HttpGet("admin/packages")]
        public async Task<IActionResult> GetAllPackagesForAdmin()
        {
            var packages = await _context.GoiDichVus
                .OrderByDescending(g => g.MaGoi)
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

        // 🌟 TẠO GÓI MỚI: POST /api/Service
        [HttpPost]
        public async Task<IActionResult> CreatePackage([FromBody] PackageRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TenGoi))
            {
                return BadRequest(new { message = "Vui lòng nhập đầy đủ thông tin tên gói!" });
            }

            // Kiểm tra ràng buộc mở khóa CV NTD
            var unlockCvPrivilege = await _context.DacQuyens.FirstOrDefaultAsync(d => d.MaCode == "NTD_UNLOCK_CV");
            if (unlockCvPrivilege != null && request.DacQuyens != null)
            {
                var cvConfig = request.DacQuyens.FirstOrDefault(d => d.MaDacQuyen == unlockCvPrivilege.MaDacQuyen);
                if (cvConfig != null && (!cvConfig.SoLuong.HasValue || cvConfig.SoLuong.Value <= 0))
                {
                    return BadRequest(new { message = "Đặc quyền mở khóa CV của Nhà tuyển dụng bắt buộc phải có số lượt cụ thể lớn hơn 0!" });
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Lưu bản ghi gói chính
                var newPackage = new GoiDichVu
                {
                    TenGoi = request.TenGoi,
                    LoaiGoi = request.LoaiGoi,
                    GiaTien = request.GiaTien,
                    GiaKhuyenMai = request.GiaKhuyenMai,
                    DonViThoiGian = request.DonViThoiGian ?? 1,
                    DoiTuongSuDung = request.DoiTuongSuDung,
                    TrangThai = true
                };

                _context.GoiDichVus.Add(newPackage);
                await _context.SaveChangesAsync();

                // 2. Lưu các đặc quyền kèm số lượng
                if (request.DacQuyens != null && request.DacQuyens.Count > 0)
                {
                    foreach (var dq in request.DacQuyens)
                    {
                        _context.Add(new GoiDichVu_DacQuyen
                        {
                            MaGoi = newPackage.MaGoi,
                            MaDacQuyen = dq.MaDacQuyen,
                            SoLuong = dq.SoLuong
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                // 🌟 Trả về Anonymous object để tránh lỗi tuần tự hóa JSON vòng lặp
                return Ok(new
                {
                    success = true,
                    message = "Thêm gói dịch vụ thành công!",
                    data = new
                    {
                        maGoi = newPackage.MaGoi,
                        tenGoi = newPackage.TenGoi,
                        giaTien = newPackage.GiaTien,
                        trangThai = newPackage.TrangThai
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Lỗi khi thêm gói: " + errorMsg });
            }
        }

        // 🌟 CẬP NHẬT GÓI: PUT /api/Service/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePackage(int id, [FromBody] PackageRequestDto request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.TenGoi))
            {
                return BadRequest(new { message = "Dữ liệu không hợp lệ!" });
            }

            var package = await _context.GoiDichVus
                .Include(g => g.GoiDichVu_DacQuyens)
                .FirstOrDefaultAsync(g => g.MaGoi == id);

            if (package == null)
            {
                return NotFound(new { message = "Không tìm thấy gói dịch vụ!" });
            }

            var unlockCvPrivilege = await _context.DacQuyens.FirstOrDefaultAsync(d => d.MaCode == "NTD_UNLOCK_CV");
            if (unlockCvPrivilege != null && request.DacQuyens != null)
            {
                var cvConfig = request.DacQuyens.FirstOrDefault(d => d.MaDacQuyen == unlockCvPrivilege.MaDacQuyen);
                if (cvConfig != null && (!cvConfig.SoLuong.HasValue || cvConfig.SoLuong.Value <= 0))
                {
                    return BadRequest(new { message = "Đặc quyền mở khóa CV của Nhà tuyển dụng bắt buộc phải có số lượt cụ thể lớn hơn 0!" });
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                package.TenGoi = request.TenGoi;
                package.LoaiGoi = request.LoaiGoi;
                package.GiaTien = request.GiaTien;
                package.GiaKhuyenMai = request.GiaKhuyenMai;
                package.DonViThoiGian = request.DonViThoiGian ?? 1;
                package.DoiTuongSuDung = request.DoiTuongSuDung;
                package.TrangThai = request.TrangThai;

                // Xóa danh sách đặc quyền cũ
                if (package.GoiDichVu_DacQuyens != null && package.GoiDichVu_DacQuyens.Count > 0)
                {
                    _context.RemoveRange(package.GoiDichVu_DacQuyens);
                    await _context.SaveChangesAsync();
                }

                // Gán danh sách đặc quyền mới
                if (request.DacQuyens != null && request.DacQuyens.Count > 0)
                {
                    foreach (var dq in request.DacQuyens)
                    {
                        _context.Add(new GoiDichVu_DacQuyen
                        {
                            MaGoi = id,
                            MaDacQuyen = dq.MaDacQuyen,
                            SoLuong = dq.SoLuong
                        });
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "Cập nhật gói dịch vụ thành công!",
                    data = new
                    {
                        maGoi = package.MaGoi,
                        tenGoi = package.TenGoi
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var errorMsg = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Lỗi khi cập nhật gói: " + errorMsg });
            }
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
                return Ok(new { message = "Đã ngưng bán gói dịch vụ này!" });
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
        // PHẦN 2: CÁC API DÀNH CHO NGƯỜI DÙNG
        // =================================================================

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

                    // 🌟 QUY TẮC ĐẶC QUYỀN AI:
                    // Nếu gói mới là Vô hạn (SoLuong == -1 hoặc NULL đối với gói không giới hạn) -> Gán -1
                    // Nếu gói mới có số lượt cụ thể (> 0):
                    //    + Nếu tài khoản trước đó ĐÃ là Vô hạn (-1) -> Giữ nguyên -1
                    //    + Nếu tài khoản trước đó có số lượt -> Cộng dồn thêm
                    bool isPackageUnlimited = !item.SoLuong.HasValue || item.SoLuong.Value == -1;

                    if (userDacQuyen != null)
                    {
                        if (isPackageUnlimited || userDacQuyen.SoLuotConLai == -1)
                        {
                            userDacQuyen.SoLuotConLai = -1; // Chuyển ngay sang Vô hạn
                        }
                        else
                        {
                            userDacQuyen.SoLuotConLai = (userDacQuyen.SoLuotConLai ?? 0) + (item.SoLuong ?? 0);
                        }
                        userDacQuyen.NgayHetHan = user.NgayHetHanGoi.Value;
                    }
                    else
                    {
                        _context.UserDacQuyens.Add(new UserDacQuyen
                        {
                            MaUser = maUser,
                            MaDacQuyen = item.MaDacQuyen,
                            SoLuotConLai = isPackageUnlimited ? -1 : item.SoLuong,
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
            catch (Exception)
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

            var aiRecords = await _context.UserDacQuyens
                .Where(ud => ud.MaUser == maUser)
                .Join(_context.DacQuyens,
                      ud => ud.MaDacQuyen,
                      dq => dq.MaDacQuyen,
                      (ud, dq) => new { ud.SoLuotConLai, ud.NgayHetHan, dq.MaCode })
                .Where(x => x.MaCode == "UV_AI_WRITE" || x.MaCode == "UV_AI_CV")
                .ToListAsync();

            int totalAiTurns = 0;
            if (aiRecords.Any(x => x.SoLuotConLai == -1 && x.NgayHetHan > DateTime.Now))
            {
                totalAiTurns = -1;
            }
            else
            {
                totalAiTurns = aiRecords
                    .Where(x => x.SoLuotConLai.HasValue && x.SoLuotConLai.Value > 0)
                    .Sum(x => x.SoLuotConLai.Value);
            }

            var allPurchasedPackages = await _context.GiaoDiches
                .Include(g => g.MaGoiNavigation)
                .Where(g => g.MaUser == maUser && g.LoaiGiaoDich == 2 && g.TrangThai == true && g.MaGoi != null)
                .OrderByDescending(g => g.NgayGd)
                .Select(g => new
                {
                    maGoi = g.MaGoi,
                    tenGoi = g.MaGoiNavigation != null ? g.MaGoiNavigation.TenGoi : "Gói VIP",
                    soTien = g.SoTien,
                    ngayMua = g.NgayGd
                })
                .ToListAsync();

            string tenGoi = "Miễn phí";
            DateTime? ngayMua = null;
            bool isVipActive = user.NgayHetHanGoi.HasValue && user.NgayHetHanGoi > DateTime.Now;

            if (isVipActive)
            {
                tenGoi = allPurchasedPackages.Count > 1
                    ? $"Đã kích hoạt ({allPurchasedPackages.Count} gói VIP)"
                    : (allPurchasedPackages.FirstOrDefault()?.tenGoi ?? "Tài khoản VIP");
                ngayMua = allPurchasedPackages.FirstOrDefault()?.ngayMua;
            }

            var activePrivileges = await _context.UserDacQuyens
                .Where(ud => ud.MaUser == maUser && ud.NgayHetHan > DateTime.Now)
                .Join(_context.DacQuyens,
                      ud => ud.MaDacQuyen,
                      dq => dq.MaDacQuyen,
                      (ud, dq) => dq.MaCode)
                .Distinct()
                .ToListAsync();

            return Ok(new
            {
                soDuVi = user.SoDuVi,
                ngayHetHanGoi = user.NgayHetHanGoi,
                luotXemCvConLai = user.LuotXemCvConLai,
                soLuotAiConLai = totalAiTurns,
                tenGoiHienTai = tenGoi,
                ngayMua = ngayMua,
                cacDacQuyen = activePrivileges,
                danhSachGoiDaMua = isVipActive ? (object)allPurchasedPackages : new List<object>()
            });
        }
    }

    public class PurchaseRequestDto
    {
        public int MaGoi { get; set; }
    }

    public class DacQuyenConfigDto
    {
        public int MaDacQuyen { get; set; }
        public int? SoLuong { get; set; }
    }

    public class PackageRequestDto
    {
        public string TenGoi { get; set; } = string.Empty;
        public byte LoaiGoi { get; set; }
        public decimal GiaTien { get; set; }
        public decimal? GiaKhuyenMai { get; set; }
        public int? DonViThoiGian { get; set; }
        public byte DoiTuongSuDung { get; set; }
        public bool TrangThai { get; set; } = true;
        public List<int>? DacQuyenIds { get; set; }
        public List<DacQuyenConfigDto>? DacQuyens { get; set; }
    }
}