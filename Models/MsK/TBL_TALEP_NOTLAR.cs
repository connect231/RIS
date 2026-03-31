using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_TALEP_NOTLAR")]
public partial class TBL_TALEP_NOTLAR
{
    [Key]
    public int LNGKOD { get; set; }

    public int? LNGTALEPKOD { get; set; }

    [Unicode(false)]
    public string? TXTNOT { get; set; }

    public int? LNGKULLANICIKOD { get; set; }

    public int? BYTDURUM { get; set; }

    [ForeignKey("LNGTALEPKOD")]
    [InverseProperty("TBL_TALEP_NOTLARs")]
    public virtual TBL_TALEP? LNGTALEPKODNavigation { get; set; }
}

