using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_MENU_YETKI_GRUBU_MENU")]
public partial class TBL_MENU_YETKI_GRUBU_MENU
{
    [Key]
    public int LNGKOD { get; set; }

    public int LNGGRUPKOD { get; set; }

    public int LNGMENUKOD { get; set; }

    [ForeignKey("LNGGRUPKOD")]
    [InverseProperty("TBL_MENU_YETKI_GRUBU_MENUs")]
    public virtual TBL_MENU_YETKI_GRUBU LNGGRUPKODNavigation { get; set; } = null!;

    [ForeignKey("LNGMENUKOD")]
    [InverseProperty("TBL_MENU_YETKI_GRUBU_MENUs")]
    public virtual TBL_MENU LNGMENUKODNavigation { get; set; } = null!;
}

