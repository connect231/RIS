using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_POS_ISLEM")]
public partial class TBL_POS_ISLEM
{
    [Key]
    public long LNGKOD { get; set; }

    [StringLength(128)]
    public string? TXTMASKKARTNO { get; set; }

    [StringLength(256)]
    public string? TXTADSOYAD { get; set; }

    [StringLength(512)]
    public string? TXTFIRMAAD { get; set; }

    public int? LNGORTAKFIRMAKOD { get; set; }

    [StringLength(512)]
    public string? TXTACIKLAMA { get; set; }

    [Column(TypeName = "money")]
    public decimal? TUTAR { get; set; }

    [StringLength(16)]
    [Unicode(false)]
    public string? GUVENLIKTIPI { get; set; }

    [StringLength(50)]
    public string? ONAY1 { get; set; }

    [StringLength(50)]
    public string? ONAY2 { get; set; }

    [StringLength(50)]
    public string? POS_ISLEMID { get; set; }

    [Column(TypeName = "money")]
    public decimal? POS_KOM_ORAN { get; set; }

    [StringLength(50)]
    public string? POS_SONUC { get; set; }

    [StringLength(256)]
    public string? POS_SONUCSTR { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? POS_ISLEM_TARIHI { get; set; }

    [StringLength(50)]
    public string? TURKPOS_RETVAL_Banka_Sonuc_kod { get; set; }

    [StringLength(50)]
    public string? TURKPOS_RETVAL_Dekont_ID { get; set; }

    [StringLength(128)]
    public string? TURKPOS_RETVAL_Ext_Data { get; set; }

    [StringLength(50)]
    public string? TURKPOS_RETVAL_GUID { get; set; }

    [StringLength(50)]
    public string? TURKPOS_RETVAL_Hash { get; set; }

    [StringLength(50)]
    public string? TURKPOS_RETVAL_Islem_ID { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TURKPOS_RETVAL_Islem_Tarih { get; set; }

    [Column(TypeName = "money")]
    public decimal? TURKPOS_RETVAL_Odeme_Tutari { get; set; }

    [StringLength(50)]
    public string? TURKPOS_RETVAL_SiparisID { get; set; }

    [StringLength(50)]
    public string? TURKPOS_RETVAL_Sonuc { get; set; }

    [StringLength(50)]
    public string? TURKPOS_RETVAL_Sonuc_Str { get; set; }

    [Column(TypeName = "money")]
    public decimal? TURKPOS_RETVAL_Tahsilat_Tutari { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TRH_CREATE_DATE { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TRH_UPDATE_DATE { get; set; }

    [StringLength(50)]
    public string? YUKLEMEYAPILDI { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? YUKLEMETARIHI { get; set; }

    [StringLength(512)]
    public string? LOGACIKLAMA { get; set; }

    [StringLength(128)]
    [Unicode(false)]
    public string? EMAIL { get; set; }

    [StringLength(512)]
    [Unicode(false)]
    public string? FATURANO { get; set; }
}

