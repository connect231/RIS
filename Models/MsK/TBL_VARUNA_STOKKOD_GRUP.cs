using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_VARUNA_STOKKOD_GRUP")]
public partial class TBL_VARUNA_STOKKOD_GRUP
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? STOCKKOD { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? TXTGRUPAD { get; set; }
}

