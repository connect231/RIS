using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_MENU")]
public partial class TBL_MENU
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(100)]
    public string TXTBASLIK { get; set; } = null!;

    [StringLength(255)]
    public string? TXTLINK { get; set; }

    [StringLength(50)]
    public string? TXTICON { get; set; }

    public int? LNGPARENTKOD { get; set; }

    public int? INTORDER { get; set; }

    public bool? AKTIF { get; set; }

    [InverseProperty("LNGPARENTKODNavigation")]
    public virtual ICollection<TBL_MENU> InverseLNGPARENTKODNavigation { get; set; } = new List<TBL_MENU>();

    [ForeignKey("LNGPARENTKOD")]
    [InverseProperty("InverseLNGPARENTKODNavigation")]
    public virtual TBL_MENU? LNGPARENTKODNavigation { get; set; }

    [InverseProperty("LNGMENUKODNavigation")]
    public virtual ICollection<TBL_MENU_YETKI_GRUBU_MENU> TBL_MENU_YETKI_GRUBU_MENUs { get; set; } = new List<TBL_MENU_YETKI_GRUBU_MENU>();
}
