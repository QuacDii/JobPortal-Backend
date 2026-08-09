using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TKVL.Models
{
    public class NganhNgheCon
    {
        [Key]
        public int MaNganhCon { get; set; }

        [Required]
        [StringLength(150)]
        public string TenNganhCon { get; set; } = string.Empty;

        // Khóa ngoại liên kết Ngành cha
        public int MaNganhCha { get; set; }

        [ForeignKey("MaNganhCha")]
        public virtual NganhNgheCha? NganhNgheChaNavigation { get; set; }

        // Liên kết đến các vị trí tuyển dụng
        public virtual ICollection<ChiTietViTri> ChiTietViTris { get; set; } = new List<ChiTietViTri>();
    }
}