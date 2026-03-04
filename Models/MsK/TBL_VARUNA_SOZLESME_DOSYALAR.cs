using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_VARUNA_SOZLESME_DOSYALAR")]
public partial class TBL_VARUNA_SOZLESME_DOSYALAR
{
    [Key]
    public int LNGKOD { get; set; }

    [StringLength(512)]
    public string? ContractId { get; set; }

    public string? FileName { get; set; }

    public string? FileBase64 { get; set; }

    public string? FileContentType { get; set; }

    public string? FileExtension { get; set; }
}
