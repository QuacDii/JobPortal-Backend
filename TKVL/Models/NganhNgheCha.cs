using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TKVL.Models
{
    public class NganhNgheCha
    {
        [Key]
        public int MaNganhCha { get; set; }

        [Required]
        [StringLength(150)]
        public string TenNganhCha { get; set; } = string.Empty;

        // Quan hệ 1 - N với ngành con
        public virtual ICollection<NganhNgheCon> NganhNgheCons { get; set; } = new List<NganhNgheCon>();
    }
}