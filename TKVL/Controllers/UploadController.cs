using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace TKVL.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UploadController : ControllerBase
    {
        private readonly Cloudinary _cloudinary;

        public UploadController(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        // 🌟 CHỈ GIỮ 1 CỔNG API DUY NHẤT TRÁNH XUNG ĐỘT ROUTE
        [HttpPost("image")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFile(IFormFile file) 
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new { success = false, message = "Tệp tin gửi lên không hợp lệ hoặc trống rỗng!" });
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".pdf", ".doc", ".docx" };
                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                {
                    return BadRequest(new { success = false, message = "Chỉ chấp nhận file Ảnh (.jpg, .png) hoặc Hồ sơ (.pdf, .doc, .docx)!" });
                }

                using var stream = file.OpenReadStream();

                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = "JobsNow/CV_Uploads"
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    return BadRequest(new { success = false, message = $"Lỗi từ Cloudinary: {uploadResult.Error.Message}" });
                }

                return Ok(new
                {
                    success = true,
                    url = uploadResult.SecureUrl.ToString(),
                    message = "Tải tệp tin lên máy chủ thành công!"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "Lỗi xử lý file!",
                    error = ex.Message
                });
            }
        }
    }
}