using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System;
using System.Threading.Tasks;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Bảo vệ API bằng JWT Token
    public class UploadController : ControllerBase
    {
        private readonly Cloudinary _cloudinary;

        // Hàm khởi tạo tiêm cấu hình cấu trúc Cloudinary tự động
        public UploadController(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        // =================================================================
        // CỔNG API: POST /api/Upload/image (Nhận Avatar & Ảnh chụp màn hình CV)
        // =================================================================
        [HttpPost("image")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            try
            {
                // 1. Kiểm tra file Frontend gửi lên có bị trống hay không
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Tệp tin gửi lên không hợp lệ hoặc trống rỗng!" });
                }

                // 2. Thiết lập thông số và thư mục lưu trữ trên Cloudinary
                var uploadResult = new ImageUploadResult();

                using (var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(file.FileName, stream),
                        Folder = "JobsNow/CVs", // Thư mục lưu trữ gọn gàng trên đám mây
                        Transformation = new Transformation().Quality("auto").FetchFormat("auto") // Tối ưu dung lượng hình ảnh
                    };

                    // Thực thi đẩy file ngầm bất đồng bộ lên Cloudinary
                    uploadResult = await _cloudinary.UploadAsync(uploadParams);
                }

                // 3. Kiểm tra xem Cloudinary có trả về lỗi SDK hay không
                if (uploadResult.Error != null)
                {
                    return BadRequest(new { success = false, message = $"Lỗi từ Cloudinary Cloud: {uploadResult.Error.Message}" });
                }

                // 4. Trả về đúng cấu trúc { url: "..." } mà Frontend đang chờ nạp
                return Ok(new
                {
                    success = true,
                    url = uploadResult.SecureUrl.ToString(),
                    message = "Tải hình ảnh lên máy chủ đám mây thành công!"
                });
            }
            catch (Exception ex)
            {
                // Bắt hoàn toàn bẫy lỗi và in ra màn hình Console của VS Code / Visual Studio để hai bạn check
                Console.WriteLine($"[CRITICAL ERROR UPLOAD]: {ex.Message}");
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi xử lý luồng ghi file hệ thống Back-end!",
                    error = ex.Message
                });
            }
        }
    }
}