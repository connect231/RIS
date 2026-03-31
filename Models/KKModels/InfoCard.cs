using System.ComponentModel.DataAnnotations;

namespace SOS.Models.KKModels
{
    public class InfoCard
    {

        [Key]
        public int Id { get; set; }
        public string kk_adsoyad { get; set; }
        public string kk_kartno { get; set; }
        public int kk_ay { get; set; }
        public int kk_yil { get; set; }
        public string kk_guvenlikkodu { get; set; }
        public decimal tutar { get; set; }
        public string firmaadi  { get; set; }
        public int ortakfirmakod { get; set; }
        public string aciklama { get; set; }
        public string sipariskodu { get; set; }
        public string telno { get; set; }

    }

   
}

