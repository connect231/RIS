using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_KULLANICI")]
public partial class TBL_KULLANICI
{
    [Key]
    public int LNGKOD { get; set; }

    public int? LNGIDENTITYKOD { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? TXTADSOYAD { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? TXTFIRMAADI { get; set; }

    public int? LNGKULLANICITIPI { get; set; }

    public int? LNGORTAKFIRMAKOD { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? TXTEMAIL { get; set; }

    public int? LNGYETKIGRUPKOD { get; set; }

    public int? LNGKULLANICITIP { get; set; }

    [InverseProperty("CreatedByNavigation")]
    public virtual ICollection<TBL_FINANS_ONAY> TBL_FINANS_ONAYCreatedByNavigations { get; set; } = new List<TBL_FINANS_ONAY>();

    [InverseProperty("RevokedByNavigation")]
    public virtual ICollection<TBL_FINANS_ONAY> TBL_FINANS_ONAYRevokedByNavigations { get; set; } = new List<TBL_FINANS_ONAY>();
}

