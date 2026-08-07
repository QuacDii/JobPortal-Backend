using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using TKVL.Models;
using System.Text.RegularExpressions;

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
            if (string.IsNullOrEmpty(request.Industry) || string.IsNullOrEmpty(request.Description))
            {
                return BadRequest(new { message = "Vui lòng cung cấp ngành nghề và mô tả." });
            }

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

                return Ok(new { data = generatedText });
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
                // 1. Lấy JD và CV
                var viTri = await _context.ChiTietViTris.FindAsync(request.MaViTri);
                if (viTri == null) return NotFound(new { message = "Không tìm thấy vị trí tuyển dụng." });

                var cv = await _context.Cvs.FindAsync(request.MaCv);
                if (cv == null) return NotFound(new { message = "Không tìm thấy hồ sơ CV." });

                // 🌟 KIỂM TRA CHẶN CV UPLOAD KHÔNG CÓ TEXT
                if (string.IsNullOrEmpty(cv.DuLieuCv))
                {
                    return BadRequest(new { message = "Tính năng AI hiện tại chỉ hỗ trợ phân tích các CV được tạo trực tiếp trên hệ thống, do CV tải lên không thể trích xuất văn bản." });
                }

                string jobDescription = $"Mô tả: {viTri.MoTaCongViec}\nYêu cầu: {viTri.YeuCauUngVien}";
                string cvContent = cv.DuLieuCv;

                // 2. Viết Prompt
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

                // 3. Bóc tách JSON
                using JsonDocument doc = JsonDocument.Parse(responseString);
                var textResult = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text").GetString();

                string cleanJson = textResult ?? "";

                // 🌟 Dùng Regex chặn mọi text thừa, chỉ quét trúng đích khối JSON
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