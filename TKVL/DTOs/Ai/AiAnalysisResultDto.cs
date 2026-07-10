namespace TKVL.DTOs.Ai
{
    public class AiAnalysisResultDto
    {
        public int DiemMatchingTong { get; set; }
        public int DiemKyNang { get; set; }
        public int DiemKinhNghiem { get; set; }
        public int DiemLinhVuc { get; set; }
        public int DiemCapBac { get; set; }
        public string DiemManhTieuBieu { get; set; } = string.Empty;
        public string DiemConThieu { get; set; } = string.Empty;
        public string ProfileExtractedJson { get; set; } = string.Empty;
    }
}