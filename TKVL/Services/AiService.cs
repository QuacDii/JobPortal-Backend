using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using TKVL.DTOs.Ai;

namespace TKVL.Services
{
    public interface IAiService
    {
        // Hàm phân tích CV dành cho Nhà tuyển dụng (Luồng nộp đơn)
        Task<AiAnalysisResultDto?> AnalyzeCvAgainstJobAsync(string cvContent, string jobDescription);

        // Hàm hỗ trợ viết/tối ưu CV dành cho Ứng viên (Luồng của Duy)
        Task<string> AssistCvWritingAsync(string currentCvContent, string targetJob);
    }

    public class AiService : IAiService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public AiService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<AiAnalysisResultDto?> AnalyzeCvAgainstJobAsync(string cvContent, string jobDescription)
        {
            // Logic cấu hình HttpClient, gửi prompt yêu cầu AI chấm điểm và trích xuất dữ liệu
            // Trả về đối tượng AiAnalysisResultDto đã map dữ liệu từ AI
            return new AiAnalysisResultDto { /* Dữ liệu phân tích */ };
        }

        public async Task<string> AssistCvWritingAsync(string currentCvContent, string targetJob)
        {
            // Cấu hình prompt gửi AI: "Dựa vào CV hiện tại và công việc mục tiêu, hãy tối ưu hóa..."
            // Trả về chuỗi văn bản chứa nội dung CV đã được AI chỉnh sửa, nâng cấp
            return "Nội dung CV đã được tối ưu hóa từ AI";
        }
    }
}