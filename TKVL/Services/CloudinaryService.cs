using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;

    public CloudinaryService(IConfiguration config)
    {
        // Khởi tạo và kết nối tài khoản Cloudinary
        var account = new Account(
            config["CloudinarySettings:CloudName"],
            config["CloudinarySettings:ApiKey"],
            config["CloudinarySettings:ApiSecret"]
        );
        _cloudinary = new Cloudinary(account);
    }

    public async Task<string> UploadImageAsync(IFormFile file)
    {
        if (file == null || file.Length == 0) return null;

        // Đọc file ảnh dưới dạng Stream dữ liệu
        using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            // Tự động cắt ảnh vuông và tối ưu dung lượng 
            Transformation = new Transformation().Width(400).Height(400).Crop("fill").Gravity("face")
        };

        var uploadResult = await _cloudinary.UploadAsync(uploadParams);

        // Trả về đường link ảnh bảo mật dạng https://res.cloudinary.com/...
        return uploadResult.SecureUrl.ToString();
    }
}