using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Keyless]
public class VIEW_CP_EXCEL_FATURA
{
    [StringLength(50)]
    public string? Fatura_No { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Fatura_Tarihi { get; set; }

    [StringLength(200)]
    public string? Ilgili_Kisi { get; set; }

    [Column(TypeName = "money")]
    public decimal? Fatura_Toplam { get; set; }

    [Column(TypeName = "money")]
    public decimal? Doviz_Tutar { get; set; }

    [StringLength(50)]
    public string? Satici_Adi { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Fatura_Vade_Tarihi { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Musteri_Odeme_Tarihi { get; set; }

    [StringLength(100)]
    public string? Proje { get; set; }

    [Column(TypeName = "money")]
    public decimal? Tahsil_Edilen { get; set; }

    [Column(TypeName = "money")]
    public decimal? Bekleyen_Bakiye { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Tahsil_Tarihi { get; set; }

    [StringLength(20)]
    public string? Durum { get; set; }

    [StringLength(20)]
    public string? Hukuki_Durum { get; set; }

    [NotMapped]
    public int? Satis_Vadesi { get; set; }

    [NotMapped]
    public int? Gecikme_Gun { get; set; }

    [NotMapped]
    public int? Bekleme_Gun { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? KayitTarihi { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? Odeme_Sozu_Tarihi { get; set; }

    // JOIN ile dolan alanlar (NotMapped — LINQ tarafında set edilir)
    [NotMapped]
    public string? MusteriUnvan { get; set; }

    [NotMapped]
    public string? UrunAdi { get; set; }

    [NotMapped]
    public decimal? Miktar { get; set; }

    [NotMapped]
    public decimal KumulatifToplam { get; set; }
}
