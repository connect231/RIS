using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_ZABBIX_HOST_LIST")]
public partial class TBL_ZABBIX_HOST_LIST
{
    [Key]
    public int LNGKOD { get; set; }

    public int? HOSTID { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? HOST { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? NAME { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? IP { get; set; }

    public int? LNGORTAKPROJEKOD { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? TXTORTAKPROJEISIM { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TRHGDT { get; set; }

    [StringLength(100)]
    public string? ACIKLAMA { get; set; }
}

