using DocumentFormat.OpenXml.Office2010.CustomUI;
using DocumentFormat.OpenXml.Presentation;
using ParamService;
using System.ServiceModel;

namespace UniCP.Services
{
    public class ParamPosService
    {
        private readonly TurkPosWSPRODSoapClient _client;

        public ParamPosService(IConfiguration config)
        {
            var binding = new BasicHttpBinding(BasicHttpSecurityMode.Transport);

            binding.SendTimeout = TimeSpan.FromMinutes(5);
            binding.ReceiveTimeout = TimeSpan.FromMinutes(5);
            binding.MaxReceivedMessageSize = 20000000;

            var endpoint = new EndpointAddress(
                config["ParamService:Url"]
            );

            _client = new TurkPosWSPRODSoapClient(binding, endpoint);
        }

        public async Task<string> HashKodAl(string data)
        {     
            return await _client.SHA2B64Async(data); 
        }


        public async Task<ST_TP_Islem_Odeme> PosOdemeAsync(ST_WS_Guvenlik G, string GUID, string KK_Sahibi, string KK_No, string KK_SK_Ay, string KK_SK_Yil, string KK_CVC, string KK_Sahibi_GSM, string Hata_URL, string Basarili_URL, string Siparis_ID, string Siparis_Aciklama, int Taksit, string Islem_Tutar, string Toplam_Tutar, string Islem_Hash, string Islem_Guvenlik_Tip, string Islem_ID, string IPAdr, string Ref_URL, string Data1, string Data2, string Data3, string Data4, string Data5, string Data6, string Data7, string Data8, string Data9, string Data10)
        {
            return await _client.Pos_OdemeAsync(G, GUID, KK_Sahibi, KK_No, KK_SK_Ay, KK_SK_Yil, KK_CVC, KK_Sahibi_GSM, Hata_URL, Basarili_URL, Siparis_ID, Siparis_Aciklama, Taksit, Islem_Tutar, Toplam_Tutar, Islem_Hash, Islem_Guvenlik_Tip, Islem_ID, IPAdr, Ref_URL, Data1, Data2, Data3, Data4, Data5, Data6, Data7, Data8, Data9, Data10);
        }

      
    }
}
