using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_TALEP_AKIS_LOG")]
public partial class TBL_TALEP_AKIS_LOG
{
    [Key]
    public int LNGKOD { get; set; }

    public int? LNGTFSNO { get; set; }

    public int? LNGSIRA { get; set; }

    public int? LNGDURUMKOD { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TRHDURUMBASLANGIC { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TRHDURUMONAY { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TRHDURUMGERIALMA { get; set; }

    public int? LNGBASLANGICKULLANICI { get; set; }

    public int? LNGONAYKULLANICI { get; set; }

    public int? LNGGERIALKULLANICI { get; set; }

    public int? LNGTALEPKOD { get; set; }

    [ForeignKey("LNGDURUMKOD")]
    [InverseProperty("TBL_TALEP_AKIS_LOGs")]
    public virtual TBL_TALEP_AKISDURUMLARI? LNGDURUMKODNavigation { get; set; }
}

