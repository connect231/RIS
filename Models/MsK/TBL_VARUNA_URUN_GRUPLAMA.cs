using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_VARUNA_URUN_GRUPLAMA")]
public partial class TBL_VARUNA_URUN_GRUPLAMA
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? TXTURUNMASK { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? TXTKOD { get; set; }

    [StringLength(128)]
    [Unicode(false)]
    public string? TXTURUNGRUP { get; set; }
}
