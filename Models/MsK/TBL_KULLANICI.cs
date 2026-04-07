using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_KULLANICI")]
public partial class TBL_KULLANICI
{
    [Key]
    public int LNGKOD { get; set; }

    public int? LNGIDENTITYKOD { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? TXTADSOYAD { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? TXTFIRMAADI { get; set; }

    public int? LNGKULLANICITIPI { get; set; }

    public int? LNGORTAKFIRMAKOD { get; set; }

    [StringLength(256)]
    [Unicode(false)]
    public string? TXTEMAIL { get; set; }

    public int? LNGYETKIGRUPKOD { get; set; }

    public int? LNGKULLANICITIP { get; set; }

}

