using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TKVL.Models;
using TKVL.DTOs;

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

        // =================================================================
        // 1. API TRẢ VỀ DANH SÁCH GỢI Ý KỸ NĂNG CHO FRONTEND
        // =================================================================
        [HttpGet("skills")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStandardSkills()
        {
            var skills = await _context.KyNangs
                .Where(k => k.TrangThai == true)
                .Select(k => new {
                    value = k.TenKyNang,
                    label = k.TenKyNang
                })
                .ToListAsync();
            return Ok(skills);
        }

        // =================================================================
        // 2. API LẤY DANH SÁCH TỈNH THÀNH & PHƯỜNG XÃ DẠNG CÂY
        // =================================================================
        [HttpGet("locations")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLocations()
        {
            var locations = await _context.ThanhPhos
                .Include(t => t.PhuongXas)
                .Select(t => new {
                    value = "TP_" + t.MaTp,
                    label = t.TenTp,
                    children = t.PhuongXas.Select(p => new {
                        value = p.MaPhuong,
                        label = p.TenPhuong
                    })
                })
                .ToListAsync();
            return Ok(locations);
        }

        // =================================================================
        // 3. API ĐĂNG TIN CHIẾN DỊCH MASTER - DETAIL
        // =================================================================
        [HttpPost("post-job")]
        public async Task<IActionResult> PostJobCampaign([FromBody] PostJobRequestDto request)
        {
            int maUser = GetCurrentUserId();

            // 1. Kiểm tra thông tin công ty và trạng thái phê duyệt
            var company = await _context.CongTies
                .FirstOrDefaultAsync(c => c.MaUser == maUser);

            if (company == null)
                return BadRequest(new { success = false, message = "Bạn chưa khởi tạo Hồ sơ doanh nghiệp!" });

            if (company.TrangThai == false)
                return BadRequest(new { success = false, message = "Hồ sơ của bạn đang chờ duyệt. Không thể đăng tin lúc này." });

            if (request.DanhSachViTri == null || request.DanhSachViTri.Count == 0)
                return BadRequest(new { success = false, message = "Vui lòng thêm ít nhất 1 vị trí công việc!" });

            // 🌟 2. Kiểm tra đặc quyền NTD_VIP_JOB còn hạn trong bảng User_DacQuyen
            bool isVipActive = await _context.UserDacQuyens
                .Include(ud => ud.DacQuyen)
                .AnyAsync(ud => ud.MaUser == maUser
                             && ud.NgayHetHan.Date >= DateTime.Now.Date
                             && ud.DacQuyen != null
                             && ud.DacQuyen.MaCode == "NTD_VIP_JOB");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var newCampaign = new TinTuyenDung
                {
                    MaCongTy = company.MaCongTy,
                    TieuDeChienDich = request.TieuDeChienDich,
                    NgayHetHan = request.NgayHetHan,
                    NgayDang = DateTime.Now,
                    TrangThai = 0, // 0: Chờ duyệt
                    IsPromoted = isVipActive // ⚡ Tự động gắn nhãn VIP/Nổi bật nếu sở hữu đặc quyền NTD_VIP_JOB
                };

                _context.TinTuyenDungs.Add(newCampaign);
                await _context.SaveChangesAsync();

                foreach (var posDto in request.DanhSachViTri)
                {
                    var newPosition = new ChiTietViTri
                    {
                        MaTin = newCampaign.MaTin,
                        TenViTri = posDto.TenViTri,
                        CapBac = posDto.CapBac,
                        SoLuongTuyen = posDto.SoLuongTuyen,
                        Luong = posDto.Luong,
                        MoTaCongViec = posDto.MoTaCongViec,
                        YeuCauUngVien = posDto.YeuCauUngVien,
                        QuyenLoi = posDto.QuyenLoi,
                        MaNganhCon = posDto.MaNganh, // 🌟 Đã sửa: MaNganh -> MaNganhCon
                        MaPhuong = posDto.MaPhuong,
                        NganhNgheKhac = posDto.NganhNgheKhac
                    };

                    _context.ChiTietViTris.Add(newPosition);
                    await _context.SaveChangesAsync();

                    if (posDto.DanhSachKyNang != null && posDto.DanhSachKyNang.Any())
                    {
                        var kyNangEntities = new List<KyNang>();
                        foreach (var tenKN in posDto.DanhSachKyNang)
                        {
                            var keyword = tenKN.Trim();
                            if (string.IsNullOrEmpty(keyword)) continue;

                            var existingSkill = await _context.KyNangs
                                .FirstOrDefaultAsync(k => k.TenKyNang.ToLower() == keyword.ToLower());

                            if (existingSkill != null)
                            {
                                kyNangEntities.Add(existingSkill);
                            }
                            else
                            {
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
                        newPosition.MaKyNangs = kyNangEntities;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Đã gửi chiến dịch thành công! Vui lòng chờ Ban quản trị duyệt tin." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi lưu chiến dịch." });
            }
        }

        // =================================================================================
        // 4. API: Lấy phễu ứng viên (Màn hình Talent Pool & Danh sách xếp hạng)
        // =================================================================================
        [HttpGet("jobs/{maViTri}/applications")]
        public async Task<IActionResult> GetApplicationsByJob(int maViTri, [FromQuery] int? minMatch, [FromQuery] int? maxMatch, [FromQuery] int? trangThai)
        {
            try
            {
                var query = _context.DonUngTuyens
                    .Include(d => d.ChiTietPhanTichAi)
                    .Include(d => d.MaCvNavigation)
                    .Where(d => d.MaViTri == maViTri)
                    .AsQueryable();

                if (minMatch.HasValue && minMatch.Value > 0)
                {
                    query = query.Where(d => d.ChiTietPhanTichAi != null && d.ChiTietPhanTichAi.DiemMatchingTong >= minMatch.Value);
                }
                if (maxMatch.HasValue && maxMatch.Value < 100)
                {
                    query = query.Where(d => d.ChiTietPhanTichAi != null && d.ChiTietPhanTichAi.DiemMatchingTong <= maxMatch.Value);
                }
                if (trangThai.HasValue)
                {
                    query = query.Where(d => d.TrangThai == trangThai.Value);
                }

                var rawList = await query
                    .OrderByDescending(d => d.ChiTietPhanTichAi != null ? d.ChiTietPhanTichAi.DiemMatchingTong : 0)
                    .ThenByDescending(d => d.NgayNop)
                    .ToListAsync();

                var listApplications = rawList.Select(d => {
                    string aiJson = d.ChiTietPhanTichAi?.ThongTinHoSoTrichXuatJson;

                    if (string.IsNullOrEmpty(aiJson) || aiJson == "{}")
                    {
                        aiJson = MapCvBuilderToAiProfileJson(d.MaCvNavigation?.DuLieuCv);
                    }

                    return new
                    {
                        maDon = d.MaDon,
                        maCv = d.MaCv,
                        ngayNop = d.NgayNop,
                        trangThai = d.TrangThai,
                        ghiChu = d.GhiChu,
                        diemMatchingTong = d.ChiTietPhanTichAi != null ? d.ChiTietPhanTichAi.DiemMatchingTong : 0,
                        profileAiJson = aiJson,
                        cvUrl = d.MaCvNavigation != null ? d.MaCvNavigation.DuongDan : null
                    };
                }).ToList();

                return Ok(new { success = true, data = listApplications });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải phễu ứng viên!", error = ex.Message });
            }
        }

        // =================================================================================
        // 5. API: Chi tiết chấm điểm AI & Bóc tách CV (Tự động Re-Analyze khi NTD nâng VIP)
        // =================================================================================
        [HttpGet("applications/{maDon}/ai-details")]
        public async Task<IActionResult> GetAiAnalysisDetail(
            int maDon,
            [FromServices] Services.IAiAnalysisService aiAnalysisService)
        {
            try
            {
                var application = await _context.DonUngTuyens
                    .Include(d => d.ChiTietPhanTichAi)
                    .Include(d => d.MaCvNavigation)
                    .Include(d => d.MaViTriNavigation)
                        .ThenInclude(v => v.MaTinNavigation)
                            .ThenInclude(t => t.MaCongTyNavigation)
                                .ThenInclude(c => c.MaUserNavigation)
                    .FirstOrDefaultAsync(d => d.MaDon == maDon);

                if (application == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn ứng tuyển!" });
                }

                if (application.TrangThai == 0)
                {
                    application.TrangThai = 1;
                    await _context.SaveChangesAsync();
                }

                var userCongTy = application.MaViTriNavigation?.MaTinNavigation?.MaCongTyNavigation?.MaUserNavigation;
                bool isVipActive = userCongTy != null
                                && userCongTy.NgayHetHanGoi.HasValue
                                && userCongTy.NgayHetHanGoi >= DateTime.Now;

                var aiData = application.ChiTietPhanTichAi;

                bool isDummyRecord = aiData == null
                                  || aiData.DiemMatchingTong == 0
                                  || aiData.ThongTinHoSoTrichXuatJson == "{}"
                                  || (aiData.DiemManhTieuBieu != null && aiData.DiemManhTieuBieu.Contains("nâng cấp gói"));

                if (isVipActive && isDummyRecord)
                {
                    bool reAnalyzeSuccess = await aiAnalysisService.AnalyzeApplicationAsync(maDon);

                    if (reAnalyzeSuccess)
                    {
                        await _context.Entry(application).Reference(d => d.ChiTietPhanTichAi).LoadAsync();
                        aiData = application.ChiTietPhanTichAi;
                    }
                }

                string extractionJson = aiData?.ThongTinHoSoTrichXuatJson;

                if (string.IsNullOrEmpty(extractionJson) || extractionJson == "{}")
                {
                    extractionJson = MapCvBuilderToAiProfileJson(application.MaCvNavigation?.DuLieuCv);
                }

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        maDon = application.MaDon,
                        cvUrl = application.MaCvNavigation?.DuongDan,
                        trangThaiHienTai = application.TrangThai,
                        ghiChuTuyenDung = application.GhiChu,
                        thuGioiThieu = application.ThuGioiThieu,
                        aiAnalysis = new
                        {
                            maPhanTich = aiData?.MaPhanTich ?? 0,
                            diemMatchingTong = aiData?.DiemMatchingTong ?? 0,
                            diemKyNang = aiData?.DiemKyNang ?? 0,
                            diemKinhNghiem = aiData?.DiemKinhNghiem ?? 0,
                            diemLinhVuc = aiData?.DiemLinhVuc ?? 0,
                            diemCapBac = aiData?.DiemCapBac ?? 0,
                            diemManhTieuBieu = aiData?.DiemManhTieuBieu ?? "Không có ghi nhận từ hệ thống",
                            diemConThieu = aiData?.DiemConThieu ?? "Không có ghi nhận từ hệ thống",
                            profileExtractedJson = extractionJson
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải chi tiết phân tích AI!", error = ex.Message });
            }
        }

        // Hàm phụ trợ: Trích xuất và cấu trúc lại chuỗi dữ liệu CV Builder
        private static string MapCvBuilderToAiProfileJson(string cvBuilderJson)
        {
            if (string.IsNullOrEmpty(cvBuilderJson)) return "{}";
            try
            {
                string cleanJson = cvBuilderJson.Trim().Replace("\uFEFF", "");

                if (cleanJson.StartsWith("\"") && cleanJson.EndsWith("\""))
                {
                    cleanJson = System.Text.RegularExpressions.Regex.Unescape(cleanJson.Substring(1, cleanJson.Length - 2));
                }

                using var doc = System.Text.Json.JsonDocument.Parse(cleanJson);
                var root = doc.RootElement;

                string hoTen = "Ứng viên hệ thống";
                string jobTitle = "Chức danh chưa rõ";
                string email = "N/A";
                string sdt = "N/A";
                string noiCuTru = "Chưa rõ";
                string hocVan = "Chưa cập nhật";
                var kyNangs = new List<string>();

                if (root.TryGetProperty("personalInfo", out var personalInfo))
                {
                    if (personalInfo.TryGetProperty("fullName", out var f)) hoTen = f.GetString() ?? hoTen;
                    if (personalInfo.TryGetProperty("jobTitle", out var j)) jobTitle = j.GetString() ?? jobTitle;
                    if (personalInfo.TryGetProperty("email", out var e)) email = e.GetString() ?? email;
                    if (personalInfo.TryGetProperty("phone", out var p)) sdt = p.GetString() ?? sdt;
                    if (personalInfo.TryGetProperty("address", out var a)) noiCuTru = a.GetString() ?? noiCuTru;
                }

                if (root.TryGetProperty("education", out var education) && education.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var item in education.EnumerateArray())
                    {
                        string school = item.TryGetProperty("school", out var sch) ? sch.GetString() : "";
                        string major = item.TryGetProperty("major", out var maj) ? maj.GetString() : "";
                        if (!string.IsNullOrEmpty(school))
                        {
                            hocVan = $"{school} - {major}";
                            break;
                        }
                    }
                }

                if (root.TryGetProperty("skills", out var skillsProp))
                {
                    string rawSkills = skillsProp.GetString() ?? "";
                    kyNangs = rawSkills.Split(new[] { '\n', ',', ';', '•', '-' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim())
                                       .Where(s => !string.IsNullOrEmpty(s) && !s.Contains("Ngôn ngữ:") && !s.Contains("Công cụ:") && !s.Contains("Định hướng:"))
                                       .ToList();
                }

                if (!kyNangs.Any()) kyNangs.Add("Hồ sơ hệ thống");

                var fallbackObj = new
                {
                    hoTen = hoTen,
                    email = email,
                    sdt = sdt,
                    viTriHienTai = jobTitle,
                    namKinhNghiem = "Xem CV gốc",
                    noiCuTru = noiCuTru,
                    kyNangNoiBat = kyNangs.ToArray(),
                    hocVan = hocVan,
                    chungChi = "Không có"
                };

                return Newtonsoft.Json.JsonConvert.SerializeObject(fallbackObj);
            }
            catch
            {
                return "{}";
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