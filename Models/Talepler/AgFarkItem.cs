namespace SOS.Models.Talepler;

public class AgFarkItem
{
    public int TfsNo { get; set; }
    public string Baslik { get; set; } = "-";
    public decimal PortalAG { get; set; }
    public decimal VarunaAG { get; set; }
    public List<string> TeklifNumaralari { get; set; } = new();
    public List<string> SiparisNumaralari { get; set; } = new();
    public List<string> FaturaReferanslari { get; set; } = new();
}

