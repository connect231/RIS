using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Keyless]
public partial class VIEW_N4B_CUSTOMER
{
    public int? Bildirim_No_Inventory { get; set; }

    public int? Envanter_No { get; set; }

    [StringLength(50)]
    public string? Sifre { get; set; }

    [StringLength(350)]
    public string? Proje { get; set; }

    [StringLength(20)]
    public string? Destek_Baslama_Tarihi { get; set; }

    [StringLength(20)]
    public string? Destek_Bitis_Tarihi { get; set; }

    [StringLength(1000)]
    public string? Firma { get; set; }

    [StringLength(1000)]
    public string? CRM_ID { get; set; }

    [StringLength(20)]
    public string? SLA_Paketi { get; set; }

    [StringLength(50)]
    public string? Musteri_Tipi1 { get; set; }

    [StringLength(20)]
    public string? Hafta_Sonu_Destek { get; set; }

    [StringLength(20)]
    public string? Mesai_Disi_Baglanti { get; set; }

    [StringLength(20)]
    public string? Cagri_Basi_Destek { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Sonislemtarih { get; set; }

    [StringLength(50)]
    public string? TfsNo { get; set; }

    [StringLength(50)]
    public string? TfsDurum { get; set; }

    [StringLength(100)]
    public string? TfsTip { get; set; }

    public int? LNGORTAKPROJEKOD { get; set; }
}
