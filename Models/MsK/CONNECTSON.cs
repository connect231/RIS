using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Keyless]
[Table("CONNECTSON")]
public partial class CONNECTSON
{
    public double? TFSNO { get; set; }

    [StringLength(255)]
    public string? MADDEBASLIK { get; set; }

    [StringLength(255)]
    public string? MADDEDURUM { get; set; }

    [StringLength(255)]
    public string? ACILMATARIHI { get; set; }

    [StringLength(255)]
    public string? ACANKULLANICI { get; set; }

    [StringLength(255)]
    public string? PROJE { get; set; }

    [StringLength(255)]
    public string? URUN { get; set; }

    [StringLength(255)]
    public string? MUSTERI_SORUMLUSU { get; set; }

    [StringLength(255)]
    public string? SATIS_SORUMLUSU { get; set; }

    [StringLength(255)]
    public string? YARATICI { get; set; }

    [StringLength(255)]
    public string? ConnectDurum { get; set; }
}

