using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("PARAMETRELER")]
public partial class PARAMETRELER
{
    [Key]
    public int Id { get; set; }

    public string? ParametreAdi { get; set; }

    public string? Deger { get; set; }

    public string? Grup { get; set; }

    public int Durum { get; set; }
}
