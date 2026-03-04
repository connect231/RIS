using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_LOG")]
public partial class TBL_LOG
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(32)]
    [Unicode(false)]
    public string? TXTTUR { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? TXTACIKLAMA { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TRHTARIH { get; set; }
}
