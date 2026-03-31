using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_N4BISSUES")]
public partial class TBL_N4BISSUE
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? TXTBILDIRIMBASLIK { get; set; }

    [Unicode(false)]
    public string? TXTBILDIRIMACIKALAMA { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? CustomerEmail { get; set; }

    public int? CategoryID { get; set; }

    public int ContactMethodID { get; set; }

    public int? IssueTypeID { get; set; }

    public int? IssueID { get; set; }

    public int? DURUM { get; set; }

    [InverseProperty("LNGTBLISSUEKODNavigation")]
    public virtual ICollection<TBL_N4BISSSEFILE> TBL_N4BISSSEFILEs { get; set; } = new List<TBL_N4BISSSEFILE>();
}

