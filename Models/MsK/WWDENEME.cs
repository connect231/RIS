using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Keyless]
public partial class WWDENEME
{
    [StringLength(6)]
    [Unicode(false)]
    public string ST { get; set; } = null!;

    public int BIR { get; set; }

    public int IKI { get; set; }
}
