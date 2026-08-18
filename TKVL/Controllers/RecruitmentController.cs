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
        // 1. API TRẢ VỀ DANH SÁCH GỢI Ý KỸ NĂNG CHO FRONTEND (CÓ HỖ TRỢ LỌC THEO NGÀNH CON)
        // =================================================================
        [HttpGet("skills")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStandardSkills([FromQuery] int? maNganhCon)
        {
            var q = _context.KyNangs.Where(k => k.TrangThai == true);

            List<string> recommended = new();
            if (maNganhCon.HasValue && maNganhCon.Value > 0)
            {
                // Lấy danh sách tên kỹ năng từng được sử dụng trong Ngành con này
                recommended = await q
                    .Where(k => k.MaViTris.Any(v => v.MaNganhCon == maNganhCon.Value))
                    .OrderBy(k => k.TenKyNang)
                    .Select(k => k.TenKyNang)
                    .Distinct()
                    .Take(15)
                    .ToListAsync();
            }

            var allSkills = await q
                .Select(k => new {
                    value = k.TenKyNang,
                    label = k.TenKyNang
                })
                .ToListAsync();

            return Ok(new
            {
                all = allSkills,
                recommended = recommended
            });
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

            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == maUser);
            if (company == null)
                return BadRequest(new { success = false, message = "Bạn chưa khởi tạo Hồ sơ doanh nghiệp!" });

            if (company.TrangThai == false)
                return BadRequest(new { success = false, message = "Hồ sơ của bạn đang chờ duyệt. Không thể đăng tin lúc này." });

            if (request.DanhSachViTri == null || request.DanhSachViTri.Count == 0)
                return BadRequest(new { success = false, message = "Vui lòng thêm ít nhất 1 vị trí công việc!" });

            // Validate hạn chót từng vị trí không được vượt quá hạn chót chiến dịch
            foreach (var pos in request.DanhSachViTri)
            {
                if (pos.NgayHetHan.HasValue && pos.NgayHetHan.Value.Date > request.NgayHetHan.Date)
                {
                    return BadRequest(new { success = false, message = $"Hạn chót của vị trí '{pos.TenViTri}' không được vượt quá hạn chót chiến dịch ({request.NgayHetHan:dd/MM/yyyy})!" });
                }
            }

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
                    TrangThai = 0, // 0: Chờ duyệt chiến dịch
                    IsPromoted = isVipActive
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
                        KinhNghiem = posDto.KinhNghiem,
                        SoLuongTuyen = posDto.SoLuongTuyen,
                        Luong = posDto.Luong,
                        MoTaCongViec = posDto.MoTaCongViec,
                        YeuCauUngVien = posDto.YeuCauUngVien,
                        QuyenLoi = posDto.QuyenLoi,
                        MaNganhCon = posDto.MaNganh,
                        MaPhuong = posDto.MaPhuong,
                        NganhNgheKhac = posDto.NganhNgheKhac,
                        TrangThai = 0, // 0: Chờ Admin duyệt vị trí này
                        NgayHetHan = posDto.NgayHetHan ?? request.NgayHetHan // Mặc định nhận hạn chót chiến dịch nếu không đặt riêng
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
                                var newSkill = new KyNang { TenKyNang = keyword, TrangThai = false };
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

        // =================================================================================
        // 6. API: Kích hoạt phân tích AI thủ công (Re-Analyze) cho một đơn ứng tuyển
        // =================================================================================
        [HttpPost("applications/{maDon}/re-analyze")]
        public async Task<IActionResult> TriggerReAnalyze(
            int maDon,
            [FromServices] Services.IAiAnalysisService aiAnalysisService)
        {
            try
            {
                var application = await _context.DonUngTuyens
                    .Include(d => d.MaViTriNavigation)
                        .ThenInclude(v => v.MaTinNavigation)
                            .ThenInclude(t => t.MaCongTyNavigation)
                    .FirstOrDefaultAsync(d => d.MaDon == maDon);

                if (application == null)
                {
                    return NotFound(new { success = false, message = "Không tìm thấy đơn ứng tuyển!" });
                }

                // Kiểm tra quyền: Nhà tuyển dụng hiện tại có phải chủ sở hữu bài đăng không?
                int currentUserId = GetCurrentUserId();
                if (application.MaViTriNavigation?.MaTinNavigation?.MaCongTyNavigation?.MaUser != currentUserId)
                {
                    return StatusCode(403, new { success = false, message = "Bạn không có quyền thao tác trên đơn ứng tuyển này!" });
                }

                // Gọi service AI phân tích lại
                bool reAnalyzeSuccess = await aiAnalysisService.AnalyzeApplicationAsync(maDon);

                if (reAnalyzeSuccess)
                {
                    return Ok(new { success = true, message = "Đã phân tích và cập nhật dữ liệu AI thành công!" });
                }

                return BadRequest(new { success = false, message = "Không thể phân tích hồ sơ lúc này. Vui lòng thử lại sau!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi phân tích AI!", error = ex.Message });
            }
        }

        // =================================================================================
        // 7. API: Lấy chi tiết chiến dịch và các vị trí để đổ vào Form chỉnh sửa (EditJob)
        // =================================================================================
        [HttpGet("job-campaign/{maTin}")]
        public async Task<IActionResult> GetJobCampaignForEdit(int maTin)
        {
            try
            {
                int currentUserId = GetCurrentUserId();
                var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == currentUserId);
                if (company == null)
                    return BadRequest(new { success = false, message = "Không tìm thấy doanh nghiệp!" });

                var campaign = await _context.TinTuyenDungs
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(v => v.MaKyNangs)
                    .FirstOrDefaultAsync(t => t.MaTin == maTin && t.MaCongTy == company.MaCongTy);

                if (campaign == null)
                    return NotFound(new { success = false, message = "Không tìm thấy chiến dịch tuyển dụng hoặc bạn không có quyền thao tác!" });

                var result = new
                {
                    maTin = campaign.MaTin,
                    tieuDeChienDich = campaign.TieuDeChienDich,
                    ngayHetHan = campaign.NgayHetHan,
                    trangThai = campaign.TrangThai,
                    danhSachViTri = campaign.ChiTietViTris.Select(v => new
                    {
                        maViTri = v.MaViTri,
                        tenViTri = v.TenViTri,
                        capBac = v.CapBac,
                        kinhNghiem= v.KinhNghiem,
                        soLuongTuyen = v.SoLuongTuyen,
                        luong = v.Luong,
                        moTaCongViec = v.MoTaCongViec,
                        yeuCauUngVien = v.YeuCauUngVien,
                        quyenLoi = v.QuyenLoi,
                        maNganh = v.MaNganhCon,
                        maPhuong = v.MaPhuong,
                        ngayHetHan = v.NgayHetHan,
                        trangThai = v.TrangThai,
                        lyDoTuChoi = v.LyDoTuChoi,
                        nganhNgheKhac = v.NganhNgheKhac,
                        danhSachKyNang = v.MaKyNangs.Select(k => k.TenKyNang).ToList()
                    }).ToList()
                };

                return Ok(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi tải dữ liệu chỉnh sửa!", error = ex.Message });
            }
        }

        // =================================================================================
        // 8. API: Cập nhật chiến dịch & gửi duyệt lại các vị trí bị từ chối/mới thêm
        // =================================================================================
        [HttpPut("update-job/{maTin}")]
        public async Task<IActionResult> UpdateJobCampaign(int maTin, [FromBody] UpdateJobRequestDto request)
        {
            int currentUserId = GetCurrentUserId();
            var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaUser == currentUserId);
            if (company == null)
                return BadRequest(new { success = false, message = "Không tìm thấy doanh nghiệp!" });

            if (request.DanhSachViTri == null || request.DanhSachViTri.Count == 0)
                return BadRequest(new { success = false, message = "Vui lòng cung cấp ít nhất 1 vị trí cần cập nhật!" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var campaign = await _context.TinTuyenDungs
                    .Include(t => t.ChiTietViTris)
                        .ThenInclude(v => v.MaKyNangs)
                    .FirstOrDefaultAsync(t => t.MaTin == maTin && t.MaCongTy == company.MaCongTy);

                if (campaign == null)
                    return NotFound(new { success = false, message = "Không tìm thấy chiến dịch tuyển dụng!" });

                // Cập nhật thông tin chiến dịch nếu có thay đổi
                if (!string.IsNullOrWhiteSpace(request.TieuDeChienDich))
                    campaign.TieuDeChienDich = request.TieuDeChienDich;
                if (request.NgayHetHan > DateTime.MinValue)
                    campaign.NgayHetHan = request.NgayHetHan;

                // Cập nhật các vị trí được gửi lên từ Form
                foreach (var posDto in request.DanhSachViTri)
                {
                    ChiTietViTri targetPosition;
                    if (posDto.MaViTri.HasValue && posDto.MaViTri.Value > 0)
                    {
                        targetPosition = campaign.ChiTietViTris.FirstOrDefault(v => v.MaViTri == posDto.MaViTri.Value);
                        if (targetPosition == null) continue;

                        // 🌟 Nếu vị trí từng bị từ chối (TrangThai = 3), đặt lại TrangThai = 0 để gửi Admin duyệt lại
                        if (targetPosition.TrangThai == 3)
                        {
                            targetPosition.TrangThai = 0;
                            targetPosition.LyDoTuChoi = null;
                        }
                    }
                    else
                    {
                        // Thêm vị trí mới vào chiến dịch nếu có
                        targetPosition = new ChiTietViTri
                        {
                            MaTin = campaign.MaTin,
                            TrangThai = 0
                        };
                        _context.ChiTietViTris.Add(targetPosition);
                    }

                    targetPosition.TenViTri = posDto.TenViTri;
                    targetPosition.CapBac = posDto.CapBac;
                    targetPosition.KinhNghiem = posDto.KinhNghiem;
                    targetPosition.SoLuongTuyen = posDto.SoLuongTuyen;
                    targetPosition.Luong = posDto.Luong;
                    targetPosition.MoTaCongViec = posDto.MoTaCongViec;
                    targetPosition.YeuCauUngVien = posDto.YeuCauUngVien;
                    targetPosition.QuyenLoi = posDto.QuyenLoi;
                    targetPosition.MaNganhCon = posDto.MaNganh;
                    targetPosition.MaPhuong = posDto.MaPhuong;
                    targetPosition.NganhNgheKhac = posDto.NganhNgheKhac;
                    targetPosition.NgayHetHan = posDto.NgayHetHan ?? campaign.NgayHetHan;

                    // Đồng bộ Kỹ năng
                    if (posDto.DanhSachKyNang != null)
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
                                var newSkill = new KyNang { TenKyNang = keyword, TrangThai = false };
                                _context.KyNangs.Add(newSkill);
                                await _context.SaveChangesAsync();
                                kyNangEntities.Add(newSkill);
                            }
                        }
                        targetPosition.MaKyNangs = kyNangEntities;
                    }
                }

                // =========================================================================
                // 🌟 TỰ ĐỘNG ĐỒNG BỘ TRẠNG THÁI CHIẾN DỊCH TỔNG (TinTuyenDung)
                // =========================================================================
                bool hasActivePosition = campaign.ChiTietViTris.Any(v => v.TrangThai == 1);
                bool hasPendingPosition = campaign.ChiTietViTris.Any(v => v.TrangThai == 0);

                if (hasActivePosition)
                {
                    // Vẫn còn ít nhất 1 vị trí đang mở nhận CV -> Chiến dịch tiếp tục hiển thị
                    campaign.TrangThai = 1;
                }
                else if (hasPendingPosition)
                {
                    // Không có vị trí nào đang mở, nhưng có vị trí vừa sửa/nộp lại chờ duyệt -> Chuyển chiến dịch về Chờ duyệt (0)
                    campaign.TrangThai = 0;
                }
                else
                {
                    // Tất cả vị trí đều đã đóng (2) hoặc bị từ chối (3) -> Chuyển chiến dịch sang Tạm dừng/Đã đóng (2)
                    campaign.TrangThai = 2;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Đã cập nhật và gửi duyệt lại vị trí thành công!" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi cập nhật vị trí!", error = ex.Message });
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