using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiHelperController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public AiHelperController(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
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
    }

    public class AiCvRequestDto
    {
        public string Industry { get; set; }
        public string Description { get; set; }
    }
}