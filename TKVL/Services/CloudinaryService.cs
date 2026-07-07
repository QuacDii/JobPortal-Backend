using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace TKVL.Services
{
    public interface ICloudinaryService
    {
        Task<string> UploadImageAsync(IFormFile file);
    }

    public class CloudinaryService : ICloudinaryService
    {
        private readonly Cloudinary _cloudinary;

        public CloudinaryService(IConfiguration config)
        {
            var acc = new Account(
                config["Cloudinary:CloudName"],
                config["Cloudinary:ApiKey"],
                config["Cloudinary:ApiSecret"]
            );
            _cloudinary = new Cloudinary(acc);
        }

        public async Task<string> UploadImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

        // Đọc file ảnh dưới dạng Stream dữ liệu
            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "JobsNow/Logos", // Tạo thư mục riêng trên Cloudinary
                Transformation = new Transformation().Width(500).Height(500).Crop("fill") // Tự động crop ảnh vuông đẹp mắt
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult.SecureUrl.ToString(); // Trả về link ảnh https
        }
    }
}