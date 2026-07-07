using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TKVL.Models
{
    public class CV_CauTrucMuc
    {
        [Key]
        public int MaCauTruc { get; set; }

        [Required]
        public int MaCV { get; set; }

        [Required]
        [MaxLength(50)]
        public string LoaiMuc { get; set; } // VD: "EXPERIENCE", "EDUCATION"

        [MaxLength(100)]
        public string TenMucHienThi { get; set; }

        public int ThuTu { get; set; }

        public bool IsVisible { get; set; }

        // Mối quan hệ (Navigation Property)
        [ForeignKey("MaCV")]
        public virtual Cv CV { get; set; }
    }
}
