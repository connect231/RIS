using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_N4BISSSEFILES")]
public partial class TBL_N4BISSSEFILE
{
    [Key]
    public int LNGKOD { get; set; }

    public int? LNGTBLISSUEKOD { get; set; }

    public string? FileName { get; set; }

    public string? FileBase64 { get; set; }

    public string? FileContentType { get; set; }

    public string? FileExtension { get; set; }

    [ForeignKey("LNGTBLISSUEKOD")]
    [InverseProperty("TBL_N4BISSSEFILEs")]
    public virtual TBL_N4BISSUE? LNGTBLISSUEKODNavigation { get; set; }
}

