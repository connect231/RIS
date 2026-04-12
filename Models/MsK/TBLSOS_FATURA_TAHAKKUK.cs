using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SOS.Models.MsK;

[Table("TBLSOS_FATURA_TAHAKKUK")]
public class TBLSOS_FATURA_TAHAKKUK
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// SAP sipariş referans numarası — Varuna SAPOutReferenceCode ile eşleşir.
    /// Tahakkuk kaydının primary lookup key'i.
    /// </summary>
    [Required, StringLength(64)]
    public string SapReferansNo { get; set; } = null!;

    /// <summary>
    /// Fatura numarası (matbu no) — opsiyonel, sonradan atanabilir.
    /// </summary>
    [StringLength(64)]
    public string? FaturaNo { get; set; }

    /// <summary>
    /// Override fatura tarihi — tüm raporlamalarda Fatura_Tarihi yerine bu kullanılır
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime TahakkukTarihi { get; set; }

    /// <summary>
    /// Audit: kayıt anındaki orijinal Fatura_Tarihi snapshot'ı
    /// </summary>
    [Column(TypeName = "datetime")]
    public DateTime? OrijinalFaturaTarihi { get; set; }

    [StringLength(500)]
    public string? Aciklama { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime OlusturulmaTarihi { get; set; }

    [StringLength(256)]
    public string? OlusturanKullanici { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? GuncellemeTarihi { get; set; }

    [StringLength(256)]
    public string? GuncelleyenKullanici { get; set; }

    /// <summary>
    /// Soft delete — false ise override yok sayılır
    /// </summary>
    public bool Aktif { get; set; } = true;
}
