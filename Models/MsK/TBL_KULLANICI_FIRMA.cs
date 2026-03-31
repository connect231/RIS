using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_KULLANICI_FIRMA")]
public partial class TBL_KULLANICI_FIRMA
{
    [Key]
    public int LNGKOD { get; set; }

    public int LNGKULLANICIKOD { get; set; }

    public int LNGFIRMAKOD { get; set; }
}

