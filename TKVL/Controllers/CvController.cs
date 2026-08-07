using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TKVL.Models;
using UglyToad.PdfPig;

namespace TKVL.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CvController : ControllerBase
    {
        private readonly JobPortalDbContext _context;

        public CvController(JobPortalDbContext context)
        {
            _context = context;
        }

        // 1. API: LẤY LINK ẢNH TỪ CV CHÍNH ĐỂ ĐỒNG BỘ AVATAR HỆ THỐNG
        [HttpGet("primary-avatar/{maUser}")]
        public async Task<IActionResult> GetPrimaryAvatar(int maUser)
        {
            try
            {
                var primaryCv = await _context.Cvs
                    .FirstOrDefaultAsync(c => c.MaUser == maUser && c.IsPrimary == true);

                if (primaryCv == null || string.IsNullOrEmpty(primaryCv.DuLieuCv))
                {
                    return Ok(new { url = "" });
                }

                using var jsonDoc = JsonDocument.Parse(primaryCv.DuLieuCv);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("personalInfo", out var personalInfo) &&
                    personalInfo.TryGetProperty("avatar", out var avatarProp))
                {
                    string avatarUrl = avatarProp.GetString();
                    return Ok(new { url = avatarUrl });
                }

                return Ok(new { url = "" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi bóc tách JSON dữ liệu CV!", error = ex.Message });
            }
        }

        // 2. API: LƯU CV (TẠO MỚI HOẶC CẬP NHẬT)
        // 2. API: LƯU CV (TẠO MỚI HOẶC CẬP NHẬT VÀ LƯU VÀO BẢNG CV_CAUTRUC)
        [HttpPost]
        public async Task<IActionResult> SaveCv([FromBody] SaveCvDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int savedCvId = 0;

                // TRƯỜNG HỢP 1: TẠO MỚI
                if (dto.MaCv == null || dto.MaCv == 0)
                {
                    var newCv = new Cv
                    {
                        MaUser = dto.MaUser,
                        MaMau = dto.MaMau,
                        MaHex = dto.MaHex,
                        TieuDe = dto.TieuDe,
                        DuLieuCv = dto.DuLieuCv,
                        IsPublic = dto.IsPublic,
                        IsPrimary = false,
                        IsUploaded = dto.IsUploaded,
                        NgayCapNhat = DateTime.Now,
                        DuongDan = dto.DuongDan,
                        FontChu = dto.FontChu,
                        NgonNgu = dto.NgonNgu,
                        CustomLayoutJson = dto.CustomLayoutJson
                    };

                    bool hasAnyCv = await _context.Cvs.AnyAsync(c => c.MaUser == dto.MaUser);
                    if (!hasAnyCv) newCv.IsPrimary = true;

                    _context.Cvs.Add(newCv);
                    await _context.SaveChangesAsync();
                    savedCvId = newCv.MaCv;
                }
                else
                {
                    // TRƯỜNG HỢP 2: CẬP NHẬT CV CŨ
                    var hasApplied = await _context.DonUngTuyens.AnyAsync(d => d.MaCv == dto.MaCv.Value);
                    if (hasApplied)
                    {
                        return BadRequest(new { success = false, message = "Do CV đã được nộp để ứng tuyển nên không thể sửa!" });
                    }

                    var existingCv = await _context.Cvs.FindAsync(dto.MaCv);
                    if (existingCv == null) return NotFound(new { success = false, message = "Không tìm thấy CV để cập nhật!" });

                    existingCv.MaMau = dto.MaMau; existingCv.MaHex = dto.MaHex; existingCv.TieuDe = dto.TieuDe;
                    existingCv.DuLieuCv = dto.DuLieuCv; existingCv.IsPublic = dto.IsPublic; existingCv.NgayCapNhat = DateTime.Now;
                    existingCv.DuongDan = dto.DuongDan; existingCv.FontChu = dto.FontChu; existingCv.CustomLayoutJson = dto.CustomLayoutJson;

                    _context.Cvs.Update(existingCv);
                    await _context.SaveChangesAsync();
                    savedCvId = existingCv.MaCv;
                }

                // ==============================================================
                // BƯỚC MỚI: BÓC TÁCH JSON VÀ LƯU VÀO BẢNG CV_CauTrucMucs
                // ==============================================================
                if (!string.IsNullOrEmpty(dto.CustomLayoutJson))
                {
                    // Xóa các cấu trúc cũ của CV này (nếu có) để chuẩn bị lưu mới
                    var oldStructures = await _context.CV_CauTrucMucs.Where(c => c.MaCV == savedCvId).ToListAsync();
                    if (oldStructures.Any())
                    {
                        _context.CV_CauTrucMucs.RemoveRange(oldStructures);
                        await _context.SaveChangesAsync();
                    }

                    // Parse chuỗi CustomLayoutJson
                    using var jsonDoc = JsonDocument.Parse(dto.CustomLayoutJson);
                    var root = jsonDoc.RootElement;
                    int orderCounter = 1;

                    // Hàm cục bộ đệ quy duyệt qua các khối children trong JSON
                    void ParseAndSaveNode(JsonElement node)
                    {
                        if (node.TryGetProperty("id", out var idProp))
                        {
                            string nodeId = idProp.GetString() ?? "";

                            // Chỉ lọc ra các mục macro lớn (Bắt đầu bằng chữ 'section-')
                            if (nodeId.StartsWith("section-") && nodeId != "section-avatar-profile" && nodeId != "section-business-card")
                            {
                                // Lấy tên hiển thị (nếu người dùng đổi tên)
                                string displayName = "";
                                if (node.TryGetProperty("children", out var children) && children.GetArrayLength() > 0)
                                {
                                    var firstChild = children[0];
                                    if (firstChild.TryGetProperty("children", out var subChildren) && subChildren.GetArrayLength() > 0)
                                    {
                                        var textNode = subChildren[0];
                                        if (textNode.TryGetProperty("content", out var contentProp))
                                        {
                                            displayName = contentProp.GetString() ?? "";
                                        }
                                    }
                                }

                                // Ghi vào Database
                                var newStructure = new CV_CauTrucMuc
                                {
                                    MaCV = savedCvId,
                                    LoaiMuc = nodeId.Replace("section-", "").ToUpper(),
                                    TenMucHienThi = string.IsNullOrEmpty(displayName) ? nodeId : displayName,
                                    ThuTu = orderCounter++,
                                    IsVisible = true 
                                };
                                _context.CV_CauTrucMucs.Add(newStructure);
                            }
                        }

                        // Đệ quy tiếp tục chui vào các nhánh con
                        if (node.TryGetProperty("children", out var childNodes))
                        {
                            foreach (var child in childNodes.EnumerateArray())
                            {
                                ParseAndSaveNode(child);
                            }
                        }
                    }

                    ParseAndSaveNode(root);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return Ok(new { success = true, message = "Lưu hồ sơ thành công!", maCv = savedCvId });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "Lỗi hệ thống khi ghi dữ liệu!", error = ex.Message });
            }
        }

        // 3. API: LẤY DANH SÁCH CV CỦA MỘT USER
        [HttpGet("user/{maUser}")]
        public async Task<IActionResult> GetUserCvs(int maUser)
        {
            try
            {
                var listCv = await _context.Cvs
                    .Where(c => c.MaUser == maUser)
                    .OrderByDescending(c => c.NgayCapNhat)
                    .Select(c => new
                    {
                        maCV = c.MaCv,
                        tieuDe = c.TieuDe,
                        isPublic = c.IsPublic,
                        duongDan = c.DuongDan,
                        isPrimary = c.IsPrimary,
                        tenMau = c.MaMauNavigation != null ? c.MaMauNavigation.TenMau : "Tiêu chuẩn",
                        ngayCapNhat = c.NgayCapNhat.HasValue ? c.NgayCapNhat.Value.ToString("dd/MM/yyyy HH:mm") : "Mới tạo"
                    })
                    .ToListAsync();

                return Ok(listCv);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi lấy danh sách CV!", error = ex.Message });
            }
        }

        // 4. API: ĐẶT CV LÀM MẶC ĐỊNH
        [HttpPut("set-primary/{maCv}")]
        public async Task<IActionResult> SetPrimaryCv(int maCv, [FromQuery] int maUser)
        {
            try
            {
                var userCvs = await _context.Cvs.Where(c => c.MaUser == maUser).ToListAsync();
                foreach (var cv in userCvs)
                {
                    cv.IsPrimary = false;
                }

                var currentCv = userCvs.FirstOrDefault(c => c.MaCv == maCv);
                if (currentCv != null)
                {
                    currentCv.IsPrimary = true;
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Đã đặt làm CV chính thức thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi đặt CV mặc định!", error = ex.Message });
            }
        }

        // 5. API: BẬT/TẮT CHẾ ĐỘ TÌM VIỆC
        [HttpPut("toggle-public/{maCv}")]
        public async Task<IActionResult> TogglePublicCv(int maCv)
        {
            try
            {
                var cv = await _context.Cvs.FindAsync(maCv);
                if (cv == null) return NotFound("Không tìm thấy CV!");

                cv.IsPublic = !cv.IsPublic;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Cập nhật trạng thái công khai thành công!", isPublic = cv.IsPublic });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi cập nhật trạng thái CV!", error = ex.Message });
            }
        }

        // 6. API: XÓA BỎ CV
        [HttpDelete("{maCv}")]
        public async Task<IActionResult> DeleteCv(int maCv)
        {
            try
            {
                var cv = await _context.Cvs.FindAsync(maCv);
                if (cv == null) return NotFound(new { success = false, message = "Không tìm thấy CV để xóa!" });

                // Chỉ chặn xóa khi đã nộp đơn để tránh lỗi khóa ngoại Foreign Key SQL
                var hasApplied = await _context.DonUngTuyens.AnyAsync(d => d.MaCv == maCv);
                if (hasApplied)
                {
                    return BadRequest(new { success = false, message = "Do CV đã được nộp để ứng tuyển nên không thể xóa!" });
                }

                int userId = cv.MaUser;
                bool wasPrimary = cv.IsPrimary;

                _context.Cvs.Remove(cv);
                await _context.SaveChangesAsync();

                // Nếu xóa CV mặc định nhưng kho vẫn còn CV khác thì tự động đôn bản ghi còn lại lên
                if (wasPrimary)
                {
                    var nextCv = await _context.Cvs
                        .Where(c => c.MaUser == userId)
                        .OrderByDescending(c => c.NgayCapNhat)
                        .FirstOrDefaultAsync();

                    if (nextCv != null)
                    {
                        nextCv.IsPrimary = true;
                        await _context.SaveChangesAsync();
                    }
                }

                return Ok(new { success = true, message = "Đã xóa CV khỏi hệ thống thành công!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi khi xóa CV!", error = ex.Message });
            }
        }

        // 7. API: LẤY CHI TIẾT 1 CV ĐỂ ĐIỀN VÀO FORM
        [HttpGet("{maCv}")]
        public async Task<IActionResult> GetCvById(int maCv)
        {
            try
            {
                var cv = await _context.Cvs.FindAsync(maCv);

                if (cv == null)
                    return NotFound(new { message = "Không tìm thấy hồ sơ CV!" });

                return Ok(cv);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi tải chi tiết CV!", error = ex.Message });
            }
        }

        // 8. API: ĐỔI TÊN CV
        [HttpPut("rename/{maCv}")]
        public async Task<IActionResult> RenameCv(int maCv, [FromBody] RenameCvDto dto)
        {
            try
            {
                // CHỐT CHẶN 3: Đã nộp đơn thì không được phép thay đổi tiêu đề hồ sơ
                var hasApplied = await _context.DonUngTuyens.AnyAsync(d => d.MaCv == maCv);
                if (hasApplied)
                {
                    return BadRequest(new { success = false, message = "Do CV đã được nộp để ứng tuyển nên không thể đổi tên!" });
                }

                var cv = await _context.Cvs.FindAsync(maCv);
                if (cv == null) return NotFound(new { success = false, message = "Không tìm thấy CV để đổi tên!" });
                if (string.IsNullOrEmpty(dto.TieuDe)) return BadRequest(new { success = false, message = "Tiêu đề không được để trống!" });

                cv.TieuDe = dto.TieuDe;
                cv.NgayCapNhat = DateTime.Now;
                _context.Cvs.Update(cv);
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "Đổi tên CV thành công!", tieuDe = cv.TieuDe });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Lỗi hệ thống khi đổi tên CV!", error = ex.Message });
            }
        }

        [HttpPost("import")]
        [Consumes("multipart/form-data")] // 🌟 Khai báo để Swagger hiểu đây là Upload File
        public IActionResult ImportCv(IFormFile file) // 🌟 BỎ [FromForm] khỏi IFormFile
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Vui lòng chọn file CV hợp lệ!" });

            if (Path.GetExtension(file.FileName).ToLower() != ".pdf")
                return BadRequest(new { message = "Hệ thống hiện chỉ hỗ trợ định dạng PDF." });

            try
            {
                using var stream = file.OpenReadStream();
                using var document = PdfDocument.Open(stream);

                if (document.Information.DocumentInformationDictionary.TryGet(UglyToad.PdfPig.Tokens.NameToken.Create("JobsNowCvData"), out var token))
                {
                    string base64Data = token is UglyToad.PdfPig.Tokens.StringToken stringToken ? stringToken.Data : token.ToString();
                    base64Data = base64Data.Trim('(', ')');

                    var bytes = Convert.FromBase64String(base64Data);
                    string jsonCvData = System.Text.Encoding.UTF8.GetString(bytes);

                    var cvObject = JsonSerializer.Deserialize<object>(jsonCvData);
                    return Ok(new { success = true, cvContent = cvObject });
                }

                return BadRequest(new { message = "Định dạng không hợp lệ. Hệ thống chỉ hỗ trợ trích xuất dữ liệu từ các file PDF được tải xuống từ JobsNow." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi khi phân tích dữ liệu file PDF: " + ex.Message });
            }
        }

        public class RenameCvDto
        {
            public string TieuDe { get; set; } = string.Empty;
        }

        // --- DTO dùng để hứng request lưu CV ---
        public class SaveCvDto
        {
            public int? MaCv { get; set; }
            public int MaUser { get; set; }
            public int MaMau { get; set; }
            public string MaHex { get; set; } = string.Empty;
            public string TieuDe { get; set; } = string.Empty;
            public bool IsUploaded { get; set; }
            public string DuLieuCv { get; set; } = string.Empty;
            public bool IsPublic { get; set; }
            public string? DuongDan { get; set; }
            public string FontChu { get; set; } = string.Empty;
            public string NgonNgu { get; set; } = "vi";
            public string? CustomLayoutJson { get; set; }
        }
    }
}