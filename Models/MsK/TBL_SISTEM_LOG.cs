using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

public partial class TBL_SISTEM_LOG
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(100)]
    public string? TXTKULLANICIADI { get; set; }

    public int? LNGKULLANICIKOD { get; set; }

    [StringLength(50)]
    public string? TXTISLEM { get; set; }

    public string? TXTDETAY { get; set; }

    [StringLength(50)]
    public string? TXTIP { get; set; }

    [StringLength(50)]
    public string? TXTMODUL { get; set; }

    public DateTime? TRHKAYIT { get; set; }
}
