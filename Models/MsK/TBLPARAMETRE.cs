using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBLPARAMETRE")]
public partial class TBLPARAMETRE
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? TXTPARAMETRE { get; set; }

    [StringLength(1024)]
    [Unicode(false)]
    public string? TXTDEGER { get; set; }

    public short? BYTGRUP { get; set; }
}

