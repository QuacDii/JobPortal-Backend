using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly CloudinaryService _cloudinaryService;

    public UploadController(CloudinaryService cloudinaryService)
    {
        _cloudinaryService = cloudinaryService;
    }

    [HttpPost("image")]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null) return BadRequest("Vui lòng chọn file ảnh hợp lệ!");

        var imageUrl = await _cloudinaryService.UploadImageAsync(file);

        if (string.IsNullOrEmpty(imageUrl))
            return StatusCode(500, "Tải ảnh lên đám mây thất bại!");

        // Trả về đường dẫn URL ảnh công khai cho Frontend hứng lấy
        return Ok(new { url = imageUrl });
    }
}