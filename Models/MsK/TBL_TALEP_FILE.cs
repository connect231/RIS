using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_TALEP_FILES")]
public partial class TBL_TALEP_FILE
{
    [Key]
    public int LNGKOD { get; set; }

    public int? LNGTALEPKOD { get; set; }

    public string? FileName { get; set; }

    public string? FileBase64 { get; set; }

    public string? FileContentType { get; set; }

    [ForeignKey("LNGTALEPKOD")]
    [InverseProperty("TBL_TALEP_FILEs")]
    public virtual TBL_TALEP? LNGTALEPKODNavigation { get; set; }
}
