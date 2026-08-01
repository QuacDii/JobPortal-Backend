using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using TKVL.Models;


namespace TKVL.Services
{
    public class AiAnalysisService : IAiAnalysisService
    {
        private readonly JobPortalDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _geminiApiKey;

        public AiAnalysisService(JobPortalDbContext context, HttpClient httpClient, IConfiguration config)
        {
            _context = context;
            _httpClient = httpClient;
            _geminiApiKey = config["GeminiSettings:ApiKey"] ?? string.Empty;
        }

        public async Task<bool> AnalyzeApplicationAsync(int maDon)
        {
            try
            {
                // 1. Lấy thông tin đơn ứng tuyển
                var don = await _context.DonUngTuyens
                    .Include(d => d.MaViTriNavigation)
                        .ThenInclude(v => v.MaTinNavigation)
                            .ThenInclude(t => t.MaCongTyNavigation)
                    .Include(d => d.MaCvNavigation)
                    .FirstOrDefaultAsync(d => d.MaDon == maDon);

                if (don == null) return false;

                // 2. Lấy thông tin User sở hữu Công ty để kiểm tra hạn VIP hiện tại
                int maUserCongTy = don.MaViTriNavigation.MaTinNavigation.MaCongTyNavigation.MaUser;
                var userCongTy = await _context.Users.FirstOrDefaultAsync(u => u.MaUser == maUserCongTy);

                bool isVipActive = userCongTy != null
                                && userCongTy.NgayHetHanGoi.HasValue
                                && userCongTy.NgayHetHanGoi >= DateTime.Now;

                // Nếu KHÔNG PHẢI VIP -> Ghi nhận record rỗng (Nếu đã có record rỗng rồi thì giữ nguyên)
                if (!isVipActive)
                {
                    await SaveOrUpdateAiResultAsync(maDon, 0, 0, 0, 0, 0,
                        "Tính năng phân tích AI chỉ áp dụng cho Nhà tuyển dụng nâng cấp gói Premium.",
                        "Vui lòng nâng cấp tài khoản doanh nghiệp để mở khóa tính năng.", "{}");
                    return true;
                }

                // 🌟 3. DỰNG NỘI DUNG JD VÀ TRÍCH XUẤT CV NỘI DUNG THÔ
                var viTri = don.MaViTriNavigation;
                string jdContent = $@"
                    Vị trí tuyển dụng: {viTri?.TenViTri}
                    Cấp bậc: {viTri?.CapBac}
                    Mô tả công việc: {viTri?.MoTaCongViec}
                    Yêu cầu ứng viên: {viTri?.YeuCauUngVien}
                    Quyền lợi: {viTri?.QuyenLoi}";

                string cvContent = string.Empty;

                if (don.MaCvNavigation != null)
                {
                    // Ưu tiên 1: Lấy chuỗi dữ liệu JSON từ CV Builder
                    if (!string.IsNullOrWhiteSpace(don.MaCvNavigation.DuLieuCv))
                    {
                        cvContent = don.MaCvNavigation.DuLieuCv;
                    }
                    // Ưu tiên 2: Nếu không có JSON, tải file PDF về và bóc tách chữ
                    else if (!string.IsNullOrWhiteSpace(don.MaCvNavigation.DuongDan))
                    {
                        cvContent = await ExtractTextFromPdfUrlAsync(don.MaCvNavigation.DuongDan);
                    }
                }

                if (string.IsNullOrWhiteSpace(cvContent))
                {
                    cvContent = "Không thể trích xuất văn bản từ CV của ứng viên.";
                }

                // 🌟 4. TRUYỀN ĐỦ 2 THAM SỐ VÀO HÀM CALL GEMINI API
                var aiResult = await CallGeminiApiAsync(cvContent, jdContent);

                if (aiResult == null)
                {
                    Console.WriteLine($"[LỖI AI]: Không thể nhận phản hồi từ Gemini API cho mã đơn {maDon}");
                    return false;
                }

                // Chuyển đổi đối tượng trích xuất thông tin sang chuỗi JSON
                string profileJson = aiResult.ThongTinHoSoTrichXuat != null
                    ? JsonConvert.SerializeObject(aiResult.ThongTinHoSoTrichXuat)
                    : "{}";

                // 🌟 5. LƯU HOẶC CẬP NHẬT (UPSERT) KẾT QUẢ VÀO DATABASE
                await SaveOrUpdateAiResultAsync(
                    maDon,
                    aiResult.DiemMatchingTong,
                    aiResult.DiemKyNang,
                    aiResult.DiemKinhNghiem,
                    aiResult.DiemLinhVuc,
                    aiResult.DiemCapBac,
                    aiResult.DiemManhTieuBieu ?? "Chưa có ghi nhận từ hệ thống.",
                    aiResult.DiemConThieu ?? "Chưa có ghi nhận từ hệ thống.",
                    profileJson
                );

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Lỗi AnalyzeApplicationAsync]: {ex.Message}");
                return false;
            }
        }

        // 🌟 HÀM HELPER XỬ LÝ UPSERT (THÊM MỚI / CẬP NHẬT BÙ)
        private async Task SaveOrUpdateAiResultAsync(
            int maDon, int diemTong, int diemKn, int diemKng, int diemLv, int diemCb,
            string diemManh, string diemThieu, string jsonProfile)
        {
            // Kiểm tra xem đơn ứng tuyển này ĐÃ CÓ record phân tích trong DB chưa
            var existingAi = await _context.ChiTietPhanTichAis
                .FirstOrDefaultAsync(a => a.MaDon == maDon);

            if (existingAi != null)
            {
                // 🔄 ĐÃ CÓ RECORD RỖNG CŨ -> CẬP NHẬT (UPDATE)
                existingAi.DiemMatchingTong = diemTong;
                existingAi.DiemKyNang = diemKn;
                existingAi.DiemKinhNghiem = diemKng;
                existingAi.DiemLinhVuc = diemLv;
                existingAi.DiemCapBac = diemCb;
                existingAi.DiemManhTieuBieu = diemManh;
                existingAi.DiemConThieu = diemThieu;
                existingAi.ThongTinHoSoTrichXuatJson = jsonProfile;

                _context.ChiTietPhanTichAis.Update(existingAi);
            }
            else
            {
                // ➕ CHƯA CÓ RECORD -> THÊM MỚI (ADD)
                var newAi = new ChiTietPhanTichAi
                {
                    MaDon = maDon,
                    DiemMatchingTong = diemTong,
                    DiemKyNang = diemKn,
                    DiemKinhNghiem = diemKng,
                    DiemLinhVuc = diemLv,
                    DiemCapBac = diemCb,
                    DiemManhTieuBieu = diemManh,
                    DiemConThieu = diemThieu,
                    ThongTinHoSoTrichXuatJson = jsonProfile
                };

                _context.ChiTietPhanTichAis.Add(newAi);
            }

            await _context.SaveChangesAsync();
        }

        // Hàm phụ: Tải PDF từ Cloudinary về và dùng PdfPig trích xuất văn bản thô
        private async Task<string> ExtractTextFromPdfUrlAsync(string url)
        {
            try
            {
                var bytes = await _httpClient.GetByteArrayAsync(url);
                using var stream = new MemoryStream(bytes);
                using var pdfDocument = PdfDocument.Open(stream);

                var textBuilder = new StringBuilder();
                foreach (var page in pdfDocument.GetPages())
                {
                    textBuilder.AppendLine(page.Text);
                }
                return textBuilder.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI ĐỌC FILE PDF CV]: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<GeminiResponseSchema?> CallGeminiApiAsync(string cvContent, string jdContent)
        {
            string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={_geminiApiKey}";

            string systemPrompt = "Bạn là một hệ thống AI sàng lọc hồ sơ tuyển dụng cao cấp (HR Tech Expert). Nhiệm vụ của bạn là đọc nội dung CV và bản mô tả công việc (JD) được cung cấp, sau đó thực hiện 2 việc:\n" +
                                  "1. Trích xuất thông tin cá nhân cơ bản từ CV của ứng viên.\n" +
                                  "2. Chấm điểm mức độ phù hợp (%) theo các tiêu chí và chỉ ra điểm mạnh, điểm thiếu sót.\n" +
                                  "BẠN BẮT BUỘC PHẢI TRẢ VỀ DỮ LIỆU DẠNG CHUỖI JSON ĐÚNG CHÍNH XÁC THEO SCHEMA SAU, KHÔNG ĐƯỢC CHÈN THÊM BẤT KỲ CHỮ DẪN GIẢI NÀO NGOÀI JSON:\n" +
                                  "{\n" +
                                  "  \"DiemMatchingTong\": kiểu số nguyên từ 0 đến 100,\n" +
                                  "  \"DiemKyNang\": kiểu số nguyên từ 0 đến 100,\n" +
                                  "  \"DiemKinhNghiem\": kiểu số nguyên từ 0 đến 100,\n" +
                                  "  \"DiemLinhVuc\": kiểu số nguyên từ 0 đến 100,\n" +
                                  "  \"DiemCapBac\": kiểu số nguyên từ 0 đến 100,\n" +
                                  "  \"DiemManhTieuBieu\": \"chuỗi văn bản liệt kê điểm mạnh dạng gạch đầu dòng, cách nhau bằng dấu xuống dòng \\n\",\n" +
                                  "  \"DiemConThieu\": \"chuỗi văn bản liệt kê điểm thiếu sót dạng gạch đầu dòng, cách nhau bằng dấu xuống dòng \\n\",\n" +
                                  "  \"ThongTinHoSoTrichXuat\": {\n" +
                                  "    \"hoTen\": \"Họ tên trích xuất được từ CV\",\n" +
                                  "    \"email\": \"Email\",\n" +
                                  "    \"sdt\": \"Số điện thoại\",\n" +
                                  "    \"viTriHienTai\": \"Chức danh hiện tại hoặc vị trí mong muốn\",\n" +
                                  "    \"namKinhNghiem\": \"Số năm kinh nghiệm làm việc (VD: 2 năm, 6 tháng...)\",\n" +
                                  "    \"noiCuTru\": \"Tỉnh/Thành phố cư trú\",\n" +
                                  "    \"kyNangNoiBat\": [\"mảng\", \"gồm\", \"các\", \"kỹ\", \"năng\"],\n" +
                                  "    \"hocVan\": \"Trường học/Bằng cấp chuyên ngành\",\n" +
                                  "    \"chungChi\": \"Các chứng chỉ đạt được (PMP, TOEIC, AWS...)\"\n" +
                                  "  }\n" +
                                  "}";

            string userContent = $"[NỘI DUNG CV ỨNG VIÊN]:\n{cvContent}\n\n[NỘI DUNG YÊU CẦU TUYỂN DỤNG (JD)]:\n{jdContent}";

            var requestBody = new
            {
                contents = new[] {
            new { parts = new[] { new { text = systemPrompt + "\n\n" + userContent } } }
        }
            };

            string jsonRequest = JsonConvert.SerializeObject(requestBody);

            int maxRetryAttempts = 3;
            int delayMilliseconds = 2000;
            HttpResponseMessage? response = null;

            try
            {
                for (int attempt = 1; attempt <= maxRetryAttempts; attempt++)
                {
                    response?.Dispose();

                    // 🌟 TẠO REQUEST MESSAGE MỚI VÀ XÓA BỎ HEADER AUTHORIZATION CỦA APP
                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                    };

                    // ⚡ XÓA SẠCH AUTHORIZATION HEADER ĐỂ GOOGLE NHẬN API KEY NẰM TRÊN URL (?key=...)
                    request.Headers.Authorization = null;
                    _httpClient.DefaultRequestHeaders.Authorization = null;

                    response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    string errText = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LOI GEMINI HTTP {response.StatusCode}] (Lần {attempt}): {errText}");

                    if (((int)response.StatusCode == 503 || (int)response.StatusCode == 429) && attempt < maxRetryAttempts)
                    {
                        await Task.Delay(delayMilliseconds);
                        delayMilliseconds *= 2;
                        continue;
                    }

                    return null;
                }

                var jsonResponse = await response!.Content.ReadAsStringAsync();

                // 🌟 LOG RESPONSE THÔ NHẬN VỀ TỪ GOOGLE
                Console.WriteLine($"[GEMINI RAW RESPONSE]: {jsonResponse}");

                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;

                if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                {
                    var candidate = candidates[0];
                    if (candidate.TryGetProperty("content", out var contentElement) &&
                        contentElement.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                    {
                        string cleanJsonText = parts[0].GetProperty("text").GetString() ?? "";

                        if (cleanJsonText.Contains("```json"))
                        {
                            cleanJsonText = cleanJsonText.Replace("```json", "").Replace("```", "").Trim();
                        }
                        else if (cleanJsonText.Contains("```"))
                        {
                            cleanJsonText = cleanJsonText.Replace("```", "").Trim();
                        }

                        return JsonConvert.DeserializeObject<GeminiResponseSchema>(cleanJsonText);
                    }
                }

                Console.WriteLine("[LOI PARSE GEMINI]: Không tìm thấy mảng 'content.parts' trong dữ liệu Google trả về.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EX IN CALL GEMINI]: {ex.Message}");
                return null;
            }
            finally
            {
                response?.Dispose();
            }
        }
    }

    // Lớp DTO trung gian đại diện cho cấu trúc JSON nhận về từ AI
    public class GeminiResponseSchema
    {
        public int DiemMatchingTong { get; set; }
        public int DiemKyNang { get; set; }
        public int DiemKinhNghiem { get; set; }
        public int DiemLinhVuc { get; set; }
        public int DiemCapBac { get; set; }
        public string DiemManhTieuBieu { get; set; }
        public string DiemConThieu { get; set; }
        public ExtractedCvProfile ThongTinHoSoTrichXuat { get; set; }
    }

    public class ExtractedCvProfile
    {
        public string hoTen { get; set; }
        public string email { get; set; }
        public string sdt { get; set; }
        public string viTriHienTai { get; set; }
        public string namKinhNghiem { get; set; }
        public string noiCuTru { get; set; }
        public string[] kyNangNoiBat { get; set; }
        public string hocVan { get; set; }
        public string chungChi { get; set; }
    }
}