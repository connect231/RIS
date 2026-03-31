using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Keyless]
[Table("TALEP_SON")]
public partial class TALEP_SON
{
    public int TFSNO { get; set; }

    [StringLength(250)]
    [Unicode(false)]
    public string? MADDEBASLIK { get; set; }

    [StringLength(256)]
    public string? MADDEDURUM { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ACILMATARIHI { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DEGISTIRMETARIHI { get; set; }

    [StringLength(256)]
    public string? ACANKULLANICI { get; set; }

    [StringLength(50)]
    public string? PROJE { get; set; }

    [StringLength(50)]
    public string? COST { get; set; }

    [StringLength(50)]
    public string? SATISDURUMU { get; set; }

    [StringLength(50)]
    public string? URUN { get; set; }

    [StringLength(50)]
    public string? MOBIL { get; set; }

    [Column(TypeName = "decimal(10, 3)")]
    public decimal? YAZILIM_TOPLAMAG { get; set; }

    [Column(TypeName = "decimal(10, 3)")]
    public decimal? TAMAMLANMA_OARANI { get; set; }

    [StringLength(1)]
    [Unicode(false)]
    public string MUSTERI_SORUMLUSU { get; set; } = null!;

    [StringLength(1)]
    [Unicode(false)]
    public string SATIS_SORUMLUSU { get; set; } = null!;

    [StringLength(256)]
    public string? YARATICI { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime PLANLANAN_PYUAT { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime GERCEKLESEN_PYUAT { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime PLANLAN_CANLITESLIM { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime GERCEKLESEN_CANLITESLIM { get; set; }
}

