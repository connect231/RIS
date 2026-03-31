using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_MENU_YETKI_GRUBU")]
public partial class TBL_MENU_YETKI_GRUBU
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(100)]
    public string TXTGRUPADI { get; set; } = null!;

    [InverseProperty("LNGGRUPKODNavigation")]
    public virtual ICollection<TBL_MENU_YETKI_GRUBU_MENU> TBL_MENU_YETKI_GRUBU_MENUs { get; set; } = new List<TBL_MENU_YETKI_GRUBU_MENU>();
}

