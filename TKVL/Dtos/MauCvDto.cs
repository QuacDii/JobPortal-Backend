namespace TKVL.Dtos
{
    public class MauCvDto
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        public bool IsATS { get; set; }
        public List<string> Colors { get; set; }
        public List<string> Categories { get; set; }
        public string? NgonNgu { get; set; }
        public string? Tags { get; set; }
        public string? DuLieuMau { get; set; }
        public string? LayoutJson { get; set; }
    }
}