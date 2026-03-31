using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Keyless]
[Table("STOKKODUESLESTIRME")]
public partial class STOKKODUESLESTIRME
{
    [StringLength(255)]
    public string? URUN_ADI { get; set; }

    [StringLength(255)]
    public string? MODUL_ADI { get; set; }

    [StringLength(255)]
    public string? STOK_KODU { get; set; }

    [StringLength(255)]
    public string? STOK_ADI { get; set; }
}

