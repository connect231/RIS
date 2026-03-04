using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_TALEP_AKISDURUMLARI")]
public partial class TBL_TALEP_AKISDURUMLARI
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(128)]
    [Unicode(false)]
    public string? TXTDURUMADI { get; set; }

    [InverseProperty("LNGDURUMKODNavigation")]
    public virtual ICollection<TBL_TALEP_AKIS_LOG> TBL_TALEP_AKIS_LOGs { get; set; } = new List<TBL_TALEP_AKIS_LOG>();
}
