using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TKVL.Models;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiHelperController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly JobPortalDbContext _context;

        public AiHelperController(IConfiguration configuration, HttpClient httpClient, JobPortalDbContext context)
        {
            _configuration = configuration;
            _httpClient = httpClient;
            _context = context;
        }

        [HttpPost("generate-cv-tips")]
        public async Task<IActionResult> GenerateCvTips([FromBody] AiCvRequestDto request)
        {
            // 1. Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrEmpty(request.Industry) || string.IsNullOrEmpty(request.Description))
            {
                return BadRequest(new { message = "Vui lòng cung cấp ngành nghề và mô tả." });
            }

            // 2. Xác định danh tính User từ Token JWT
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)
                           ?? User.Claims.FirstOrDefault(c => c.Type == "nameid")
                           ?? User.Claims.FirstOrDefault(c => c.Type == "sub");

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int maUser))
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập để sử dụng tính năng AI!" });
            }

            // 3. Kiểm tra quyền hạn & số lượt AI trong User_DacQuyen
            var aiRecords = await _context.UserDacQuyens
                .Where(ud => ud.MaUser == maUser)
                .Join(_context.DacQuyens,
                      ud => ud.MaDacQuyen,
                      dq => dq.MaDacQuyen,
                      (ud, dq) => new { UserDacQuyen = ud, dq.MaCode })
                .Where(x => x.MaCode == "UV_AI_WRITE" || x.MaCode == "UV_AI_CV")
                .ToListAsync();

            bool isUnlimitedActive = aiRecords.Any(x => x.UserDacQuyen.SoLuotConLai == -1 && x.UserDacQuyen.NgayHetHan > DateTime.Now);
            var finiteCreditRecord = aiRecords.FirstOrDefault(x => x.UserDacQuyen.SoLuotConLai.HasValue && x.UserDacQuyen.SoLuotConLai.Value > 0)?.UserDacQuyen;

            if (!isUnlimitedActive && finiteCreditRecord == null)
            {
                return BadRequest(new { message = "Bạn đã hết lượt sử dụng AI. Vui lòng nâng cấp tài khoản hoặc mua thêm lượt!" });
            }

            // 4. Cấu hình & gọi Google Gemini API
            string apiKey = _configuration["GeminiSettings:ApiKey"]?.Trim();
            if (string.IsNullOrEmpty(apiKey))
            {
                return StatusCode(500, new { message = "Thiếu cấu hình API Key của Gemini trong appsettings.json" });
            }

            string prompt = $@"Bạn là một chuyên gia tuyển dụng nhân sự cấp cao. Hãy viết nội dung CV cho ngành ""{request.Industry}"" dựa trên mô tả sau của ứng viên: ""{request.Description}"". 
            Hãy viết làm 2 phần rõ ràng:
            1. Mục tiêu nghề nghiệp (1 đoạn văn ngắn gọn, chuyên nghiệp).
            2. Kinh nghiệm làm việc nổi bật (3-4 gạch đầu dòng mô tả công việc súc tích, có dùng số liệu nếu có).
            Không giải thích gì thêm, chỉ in ra kết quả.";

            var requestBody = new
            {
                contents = new[] {
                    new { parts = new[] { new { text = prompt } } }
                }
            };

            var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
            string geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={apiKey}";

            var response = await _httpClient.PostAsync(geminiUrl, content);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[LỖI GOOGLE GEMINI]: {responseString}");
                return StatusCode(500, new { message = $"Lỗi từ Google: {responseString}" });
            }

            using JsonDocument doc = JsonDocument.Parse(responseString);
            try
            {
                var generatedText = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                // 5. Trừ 1 lượt nếu đang sử dụng lượt hữu hạn
                if (!isUnlimitedActive && finiteCreditRecord != null)
                {
                    finiteCreditRecord.SoLuotConLai -= 1;
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    success = true,
                    data = generatedText
                });
            }
            catch
            {
                return StatusCode(500, new { message = "Không thể đọc kết quả từ AI." });
            }
        }

        [HttpPost("analyze-fit")]
        public async Task<IActionResult> AnalyzeFitBeforeApply([FromBody] AnalyzeFitRequestDto request)
        {
            try
            {
                var viTri = await _context.ChiTietViTris.FindAsync(request.MaViTri);
                if (viTri == null) return NotFound(new { message = "Không tìm thấy vị trí tuyển dụng." });

                var cv = await _context.Cvs.FindAsync(request.MaCv);
                if (cv == null) return NotFound(new { message = "Không tìm thấy hồ sơ CV." });

                if (string.IsNullOrEmpty(cv.DuLieuCv))
                {
                    return BadRequest(new { message = "Tính năng AI hiện tại chỉ hỗ trợ phân tích các CV được tạo trực tiếp trên hệ thống, do CV tải lên không thể trích xuất văn bản." });
                }

                string jobDescription = $"Mô tả: {viTri.MoTaCongViec}\nYêu cầu: {viTri.YeuCauUngVien}";
                string cvContent = cv.DuLieuCv;

                string prompt = $@"
            Bạn là một hệ thống ATS đánh giá CV. Hãy so sánh độ phù hợp của Hồ sơ ứng viên (CV) so với Yêu cầu công việc (JD).
            
            THÔNG TIN CÔNG VIỆC (JD):
            {jobDescription}

            HỒ SƠ ỨNG VIÊN (CV):
            {cvContent}

            YÊU CẦU:
            Trả về kết quả DƯỚI DẠNG JSON TUYỆT ĐỐI CHÍNH XÁC theo cấu trúc sau (không kèm mã markdown):
            {{
                ""diemPhuHop"": [Số nguyên từ 0 đến 100],
                ""danhGia"": ""[Nhận xét ngắn gọn dưới 50 chữ về điểm mạnh, yếu]""
            }}";

                string apiKey = _configuration["GeminiSettings:ApiKey"]?.Trim();
                if (string.IsNullOrEmpty(apiKey)) return StatusCode(500, new { message = "Thiếu cấu hình API Key." });

                string geminiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={apiKey}";
                var requestBody = new { contents = new[] { new { parts = new[] { new { text = prompt } } } } };
                var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(geminiUrl, content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode(500, new { message = "Lỗi kết nối đến Google Gemini AI." });

                using JsonDocument doc = JsonDocument.Parse(responseString);
                var textResult = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                string cleanJson = textResult ?? "";

                var match = Regex.Match(cleanJson, @"\{.*\}", RegexOptions.Singleline);
                if (match.Success)
                {
                    cleanJson = match.Value;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var aiResult = JsonSerializer.Deserialize<AiResultDto>(cleanJson, options);

                return Ok(new { success = true, data = aiResult });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi hệ thống hoặc định dạng AI trả về không chuẩn.", error = ex.Message });
            }
        }
    }

    public class AnalyzeFitRequestDto
    {
        public int MaViTri { get; set; }
        public int MaCv { get; set; }
    }

    public class AiResultDto
    {
        [System.Text.Json.Serialization.JsonPropertyName("diemPhuHop")]
        public int DiemPhuHop { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("danhGia")]
        public string DanhGia { get; set; } = string.Empty;
    }

    public class AiCvRequestDto
    {
        public string Industry { get; set; }
        public string Description { get; set; }
    }
}