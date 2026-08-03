namespace TKVL.Models
{
    public partial class UngVienDaLuu
    {
        public int MaUser { get; set; }
        public int MaCv { get; set; }
        public DateTime NgayLuu { get; set; } = DateTime.Now;
        public string? GhiChuCaNhan { get; set; }

        public virtual User MaUserNavigation { get; set; } = null!;
        public virtual Cv MaCvNavigation { get; set; } = null!;
    }
}
