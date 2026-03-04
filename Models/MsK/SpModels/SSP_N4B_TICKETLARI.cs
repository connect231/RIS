namespace UniCP.Models.MsK.SpModels
{
    public class SSP_N4B_TICKETLARI
    {
        public int Bildirim_No { get; set; }
        public string? Bildirim_Tipi { get; set; } 
        public string? Bildirim_Durumu { get; set; }
        public string? Gelis_Kanali { get; set; }
        public string? Bildirim_Aciklamasi { get; set; }
        public DateTime Bildirim_Tarihi { get; set; }
        public string? Musteri_Tipi1 { get; set; }
        public string? Firma { get; set; }
        public DateTime Sonislemtarih { get; set; }
        public int Sla { get; set; }
        public decimal? SLA_YD_Cozum_Sure { get; set; }
        public decimal? SLA_YD_Cozum_Kalan_Sure { get; set; }
        public string? Bildirim_Bekletme_Neden { get; set; }
        
    }
}
