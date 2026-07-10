using System.Threading.Tasks;

namespace TKVL.Services
{
    public interface IAiAnalysisService
    {
        Task<bool> AnalyzeApplicationAsync(int maDon);
    }
}