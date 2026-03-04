using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_TALEP")]
public partial class TBL_TALEP
{
    [Key]
    public int LNGKOD { get; set; }

    public int? LNGPROJEKOD { get; set; }

    public string? TXTTALEPBASLIK { get; set; }

    public string? TXTTALEPACIKLAMA { get; set; }

    public int? LNGTFSNO { get; set; }

    public int? LNGVARUNAKOD { get; set; }

    [StringLength(10)]
    public string? BYTDURUM { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? DEC_EFOR { get; set; }

    [Unicode(false)]
    public string? TXT_SORUMLULAR { get; set; }

    [StringLength(50)]
    public string? TXT_PO { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TRHKAYIT { get; set; }

    public int? INT_ANKET_PUAN { get; set; }

    [StringLength(500)]
    public string? TXT_ANKET_NOT { get; set; }

    [InverseProperty("LNGTALEPKODNavigation")]
    public virtual ICollection<TBL_TALEP_FILE> TBL_TALEP_FILEs { get; set; } = new List<TBL_TALEP_FILE>();

    [InverseProperty("LNGTALEPKODNavigation")]
    public virtual ICollection<TBL_TALEP_NOTLAR> TBL_TALEP_NOTLARs { get; set; } = new List<TBL_TALEP_NOTLAR>();
}
