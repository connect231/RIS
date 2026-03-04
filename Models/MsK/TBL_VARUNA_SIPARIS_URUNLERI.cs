using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_VARUNA_SIPARIS_URUNLERI")]
public partial class TBL_VARUNA_SIPARIS_URUNLERI
{
    [Key]
    public int LNGKOD { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DeliveryTime { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TransactionDate { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string? PRODUCTSID { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string? CrmOrderId { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string? StockId { get; set; }

    [Column(TypeName = "money")]
    public decimal? Quantity { get; set; }

    [Column(TypeName = "money")]
    public decimal? LineDiscountRate { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string? StockUnitType { get; set; }

    [Column(TypeName = "money")]
    public decimal? Tax { get; set; }

    [StringLength(64)]
    [Unicode(false)]
    public string? PYPSapId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedOn { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ModifiedOn { get; set; }

    [Column(TypeName = "money")]
    public decimal? UnitPrice { get; set; }

    [Column(TypeName = "money")]
    public decimal? Total { get; set; }

    [Column(TypeName = "money")]
    public decimal? NetLineTotalWithTax { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? ProductName { get; set; }

    [StringLength(128)]
    [Unicode(false)]
    public string? StockCode { get; set; }

    [StringLength(32)]
    [Unicode(false)]
    public string? ItemNo { get; set; }
}
