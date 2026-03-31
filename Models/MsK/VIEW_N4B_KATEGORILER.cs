using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Keyless]
public partial class VIEW_N4B_KATEGORILER
{
    public int? CategoryID { get; set; }

    [StringLength(200)]
    public string? CategoryName { get; set; }

    [StringLength(200)]
    public string? ParentCategoryID { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Sonislemtarih { get; set; }

    public byte UnDeleted { get; set; }
}

