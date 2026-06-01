using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TKVL.Models;

public partial class MauSac_MauCV
{
    [Key]
    public int MaMauSac { get; set; }

    public int MaMau { get; set; }

    [Required]
    [StringLength(10)]
    public string MaHex { get; set; } = null!;

    [ForeignKey("MaMau")]
    public virtual MauCV MauCVNavigation { get; set; } = null!;
}