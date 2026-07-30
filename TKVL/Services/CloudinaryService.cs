using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading.Tasks;

namespace TKVL.Services
{
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

        // Upload Logo công ty (Có crop vuông)
        public async Task<string> UploadLogoAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            using var stream = file.OpenReadStream();
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "JobsNow/Logos",
                Transformation = new Transformation().Width(500).Height(500).Crop("fill")
            };

            var uploadResult = await _cloudinary.UploadAsync(uploadParams);
            return uploadResult?.SecureUrl?.ToString();
        }

        // Upload Giấy phép kinh doanh (Giữ nguyên gốc 100%, bật Public Access)
        public async Task<string> UploadGpkdAsync(IFormFile file)
        {
            if (file == null || file.Length == 0) return null;

            using var stream = file.OpenReadStream();

            // Đưa tất cả (kể cả PDF) về ImageUploadParams để Cloudinary cấp quyền Public và hỗ trợ Convert ảnh
            var imageParams = new ImageUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = "JobsNow/GPKD",
                AccessMode = "public"
            };

            var uploadResult = await _cloudinary.UploadAsync(imageParams);
            return uploadResult?.SecureUrl?.ToString();
        }
    }
}