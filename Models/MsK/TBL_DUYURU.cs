using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SOS.Models.MsK;

[Table("TBL_DUYURU")]
public class TBL_DUYURU
{
    [Key]
    public int Id { get; set; }

    /// <summary>Orijinal dosya adı (gösterim için)</summary>
    [Required, MaxLength(255)]
    public string PdfDosyaAdi { get; set; } = null!;

    /// <summary>PDF içeriği Base64 olarak saklanır</summary>
    [Required]
    public string PdfIcerik { get; set; } = null!;

    public DateTime BaslangicTarihi { get; set; }

    public DateTime BitisTarihi { get; set; }

    public DateTime EklenmeTarihi { get; set; } = DateTime.Now;

    /// <summary>Yükleyen kullanıcının TBL_KULLANICI.LNGKOD değeri</summary>
    public int? EkleyenKullaniciId { get; set; }
}

