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
                // 1. Lấy thông tin Đơn ứng tuyển kèm liên kết ngược về bảng chiến dịch TinTuyenDung
                var application = await _context.DonUngTuyens
                    .Include(d => d.MaCvNavigation)
                    .Include(d => d.MaViTriNavigation)
                        .ThenInclude(v => v.MaTinNavigation)
                    .FirstOrDefaultAsync(d => d.MaDon == maDon);

                if (application == null || application.MaCvNavigation == null || application.MaViTriNavigation == null)
                    return false;

                // 2. Truy vấn thông tin hồ sơ Công ty để lấy mã User kiểm tra giao dịch
                var maCongTy = application.MaViTriNavigation.MaTinNavigation.MaCongTy;
                var company = await _context.CongTies.FirstOrDefaultAsync(c => c.MaCongTy == maCongTy);
                if (company == null) return false;

                // Lấy giao dịch MUA GÓI thành công mới nhất (Bắt buộc g.MaGoi phải có giá trị)
                var daMuaGoi = await _context.GiaoDiches
                    .Include(g => g.MaGoiNavigation)
                    .Where(g => g.MaUser == company.MaUser && g.TrangThai == true && g.MaGoi != null)
                    .OrderByDescending(g => g.NgayGd)
                    .FirstOrDefaultAsync();

                bool isPremium = false;

                // Kiểm tra thực thể gói điều hướng có tồn tại hay không
                if (daMuaGoi != null && daMuaGoi.MaGoiNavigation != null)
                {
                    var goiDichVu = daMuaGoi.MaGoiNavigation;

                    // Đối chiếu khớp với ID gói 3 (LoaiGoi = 2, DonViThoiGian = 6) trong DB của bác
                    if (goiDichVu.LoaiGoi == 2 && goiDichVu.DonViThoiGian == 6)
                    {
                        isPremium = true;
                    }
                    // Đối chiếu khớp với ID gói 4 (LoaiGoi = 3, DonViThoiGian = 1) trong DB của bác
                    else if (goiDichVu.LoaiGoi == 3 && goiDichVu.DonViThoiGian == 1)
                    {
                        isPremium = true;
                    }
                }

                // ===================================================================
                // KỊCH BẢN 1: Nhà tuyển dụng không dùng gói Premium -> Ghi nhận bản ghi trống
                // ===================================================================
                if (!isPremium)
                {
                    var emptyAnalysis = new ChiTietPhanTichAi
                    {
                        MaDon = maDon,
                        DiemMatchingTong = 0,
                        DiemKyNang = 0,
                        DiemKinhNghiem = 0,
                        DiemLinhVuc = 0,
                        DiemCapBac = 0,
                        DiemManhTieuBieu = "Tính năng phân tích AI chỉ áp dụng cho Nhà tuyển dụng nâng cấp gói Premium.",
                        DiemConThieu = "Vui lòng nâng cấp tài khoản doanh nghiệp để mở khóa tính năng.",
                        ThongTinHoSoTrichXuatJson = "{}"
                    };

                    _context.ChiTietPhanTichAis.Add(emptyAnalysis);
                    await _context.SaveChangesAsync();
                    return true;
                }

                // ===================================================================
                // KỊCH BẢN 2: Đủ điều kiện gói Premium -> Tiến hành bóc tách và gọi AI
                // ===================================================================
                string cvText = "";
                var cv = application.MaCvNavigation;
                if (!string.IsNullOrEmpty(cv.DuLieuCv))
                {
                    cvText = cv.DuLieuCv;
                }
                else if (!string.IsNullOrEmpty(cv.DuongDan))
                {
                    cvText = await ExtractTextFromPdfUrlAsync(cv.DuongDan);
                }

                if (string.IsNullOrEmpty(cvText)) cvText = "Hồ sơ trống hoặc không thể bóc tách văn bản.";

                var position = application.MaViTriNavigation;
                string jdText = $"Tên vị trí: {position.TenViTri}\n" +
                               $"Mức lương: {position.Luong}\n" +
                               $"Mô tả công việc: {position.MoTaCongViec}\n" +
                               $"Yêu cầu ứng viên: {position.YeuCauUngVien}\n" +
                               $"Quyền lợi: {position.QuyenLoi}";

                // Thực hiện gửi dữ liệu lên API Gemini
                var aiResult = await CallGeminiApiAsync(cvText, jdText);

                if (aiResult != null)
                {
                    var jsonSettings = new JsonSerializerSettings
                    {
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    };

                    var analysis = new ChiTietPhanTichAi
                    {
                        MaDon = maDon,
                        DiemMatchingTong = aiResult.DiemMatchingTong,
                        DiemKyNang = aiResult.DiemKyNang,
                        DiemKinhNghiem = aiResult.DiemKinhNghiem,
                        DiemLinhVuc = aiResult.DiemLinhVuc,
                        DiemCapBac = aiResult.DiemCapBac,
                        DiemManhTieuBieu = aiResult.DiemManhTieuBieu,
                        DiemConThieu = aiResult.DiemConThieu,
                        ThongTinHoSoTrichXuatJson = JsonConvert.SerializeObject(aiResult.ThongTinHoSoTrichXuat, jsonSettings)
                    };

                    _context.ChiTietPhanTichAis.Add(analysis);
                    await _context.SaveChangesAsync();
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LỖI PHÂN TÍCH AI SYSTEM]: {ex.Message}");
                return false;
            }
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

        // Hàm phụ: Gửi dữ liệu sang mô hình Gemini Lite và tự động xử lý giải phóng luồng khi gặp lỗi quá tải
        private async Task<GeminiResponseSchema> CallGeminiApiAsync(string cvContent, string jdContent)
        {
            // Thay đổi định danh sang dòng gemini-3.1-flash-lite để tránh tình trạng nghẽn mạch 503 của máy chủ Google
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

            int maxRetryAttempts = 4;
            int delayMilliseconds = 3000;
            HttpResponseMessage response = null;

            try
            {
                for (int attempt = 1; attempt <= maxRetryAttempts; attempt++)
                {
                    response?.Dispose();

                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    response = await _httpClient.PostAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        break;
                    }

                    if (((int)response.StatusCode == 503 || (int)response.StatusCode == 429) && attempt < maxRetryAttempts)
                    {
                        Console.WriteLine($"[CANH BAO AI]: Mo hinh ban hoac dat gioi han tan suat o luot thu {attempt}. Tien hanh thu lai sau {delayMilliseconds / 1000} giay...");
                        await Task.Delay(delayMilliseconds);
                        delayMilliseconds *= 2;
                        continue;
                    }

                    string errorResponse = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[LOI TU GOOGLE GEMINI]: {errorResponse}");
                    return null;
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                string cleanJsonText = root.GetProperty("candidates")[0]
                                       .GetProperty("content")
                                       .GetProperty("parts")[0]
                                       .GetProperty("text").GetString();

                if (string.IsNullOrEmpty(cleanJsonText)) return null;

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