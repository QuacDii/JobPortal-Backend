using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace TKVL.Services
{
    public interface ICloudinaryService
    {
        Task<string> UploadLogoAsync(IFormFile file);
        Task<string> UploadGpkdAsync(IFormFile file);
    }
}