using DocumentFormat.OpenXml.Office2010.CustomUI;
using Microsoft.AspNetCore.Mvc;
using ParamService;
using ParamService;
using System.ServiceModel;
using System.ServiceModel;
using UniCP.Controllers.N4B;
using UniCP.DbData;
using UniCP.Models.KKModels;
using UniCP.Models.MsK;
using Microsoft.EntityFrameworkCore;
using UniCP.Services;




namespace UniCP.Controllers.KKodeme
{



    public class KKController : Controller
    {
        private readonly MskDbContext db;
       
        private static readonly HttpClient _externalClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        private readonly ParamPosService _paramService;

        public KKController(MskDbContext mskDb, ParamPosService paramService)
        {
            db = mskDb;
            _paramService= paramService;


        }

        public ActionResult OdemeAl()
        {


            return View();
        }

            [HttpPost]
            [ActionName("OdemeAl")]
        public async Task<ActionResult> OdemeAlAsync(InfoCard kb)
        {

            try
            {
                string client_code = db.TBLPARAMETREs.Where(x => x.TXTPARAMETRE == "CLIENT_CODE" && x.BYTGRUP == 1000).Select(i => i.TXTDEGER).FirstOrDefault();
                string client_username = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "CLIENT_USERNAME" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
                string client_password = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "CLIENT_PASSWORD" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
                string guidsi = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "GUID" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
                string basarili_url = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "BASARILI_URL" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
                string basarisiz_url = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "BASARISIZ_URL" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
                string istek_url = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "ISTEK_URL" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
                string kdv = db.TBLPARAMETREs.Where(x => x.TXTPARAMETRE == "KDVORANI").Select(i => i.TXTDEGER).FirstOrDefault();
                decimal kdv_orani = Convert.ToDecimal(kdv);

                string tutar = String.Format("{0:0.00;-0.00;0}", kb.tutar);
                int taksit = 1;
                string kk_securetype = "3D";


                string maskkart = kb.kk_kartno.Substring(0, 8) + "******" + kb.kk_kartno.Substring(14, 2);

                TBL_POS_ISLEM pos_islemT = new TBL_POS_ISLEM();

                pos_islemT.TXTMASKKARTNO= maskkart;
                pos_islemT.TXTADSOYAD = kb.kk_adsoyad;
                pos_islemT.TXTFIRMAAD = kb.firmaadi;
                pos_islemT.LNGORTAKFIRMAKOD = kb.ortakfirmakod;
                pos_islemT.TXTACIKLAMA = kb.aciklama;            
                pos_islemT.TUTAR = kb.tutar;
                pos_islemT.POS_ISLEM_TARIHI = DateTime.Now;


                db.TBL_POS_ISLEMs.Add(pos_islemT);
                db.SaveChanges();

                string siparis_ID = pos_islemT.LNGKOD.ToString();
                string hashkod = "";

                try
                {
                    //hash kodu alınıyor
                    //CLIENT_CODE & GUID & Taksit & Islem_Tutar & Toplam_Tutar & Siparis_ID & Hata_URL & Basarili_URL
                    string data = client_code + guidsi + taksit.ToString() + tutar + tutar + siparis_ID + basarisiz_url + basarili_url;

                    hashkod = await _paramService.HashKodAl(data);

                    pos_islemT.POS_SONUC = hashkod;
                    db.TBL_POS_ISLEMs.Update(pos_islemT);
                    db.SaveChanges();
                }
                catch (Exception ex)
                {
                    string mesaj = "Hash Kod Alınırken Hata Alındı Mesaj : " + ex.Message;

                    pos_islemT.POS_SONUC = mesaj;
                    db.TBL_POS_ISLEMs.Update(pos_islemT);
                    db.SaveChanges();

                    throw;
                }


                try
                {
                    // ilk kontrol ve 3d yön linki için servise gidiliyor

                    ST_WS_Guvenlik gvn = new ST_WS_Guvenlik();
                    gvn.CLIENT_CODE = client_code;
                    gvn.CLIENT_USERNAME = client_username;
                    gvn.CLIENT_PASSWORD = client_password;

                  


                    var sonuc = await _paramService.PosOdemeAsync(gvn, guidsi, kb.kk_adsoyad, kb.kk_kartno, kb.kk_ay.ToString(), kb.kk_yil.ToString(), kb.kk_guvenlikkodu, kb.telno,
                        basarisiz_url, basarili_url, siparis_ID, kb.aciklama, Convert.ToInt16(taksit), tutar, tutar, hashkod, kk_securetype, "3", "127.0.0.1", "", "", "", "", "", "", "", "", "", "", "");


                    if (sonuc.Sonuc == "1" && sonuc.Sonuc_Str == "İşlem Başarılı" && kk_securetype == "3D")
                    {
                        pos_islemT.POS_SONUC = sonuc.Sonuc;
                        pos_islemT.POS_SONUCSTR = sonuc.Sonuc_Str;
                        pos_islemT.GUVENLIKTIPI = kk_securetype;
                        pos_islemT.POS_ISLEMID = sonuc.Islem_ID.ToString();
                        pos_islemT.POS_KOM_ORAN = Convert.ToDecimal(sonuc.Komisyon_Oran);
                        db.TBL_POS_ISLEMs.Update(pos_islemT);
                        db.SaveChanges();


                        string url = sonuc.UCD_URL;

                        string jsonG = System.Text.Json.JsonSerializer.Serialize(new PosOdemeCookieModel() { ST_TP_Islem_Odeme = sonuc });
                        Response.Cookies.Append("PosOdemeCookie", jsonG, new CookieOptions() { Expires = DateTime.Now.AddMinutes(3) });
                        return Redirect(sonuc.UCD_URL);

                       
                    }
                    else
                    {
                        pos_islemT.POS_SONUC = sonuc.Sonuc;
                        pos_islemT.POS_SONUCSTR = sonuc.Sonuc_Str;
                       

                        db.TBL_POS_ISLEMs.Update(pos_islemT);
                        db.SaveChanges();

                    }


                    }
                catch (Exception)
                {

                    throw;
                }
             
            }
            catch (Exception)
            {

                throw;
            }

         
               

            return Ok();
        }


        [HttpPost]
        public IActionResult Basarili([FromForm] Pos_OdemePostDTO pos_OdemePostDTO)
        {
            var kyt = db.TBL_POS_ISLEMs.Where(i=>i.LNGKOD == Convert.ToInt32(pos_OdemePostDTO.TURKPOS_RETVAL_Siparis_ID)).FirstOrDefault();

            kyt.TURKPOS_RETVAL_Banka_Sonuc_kod= pos_OdemePostDTO.TURKPOS_RETVAL_Banka_Sonuc_Kod;
            kyt.TURKPOS_RETVAL_Dekont_ID= pos_OdemePostDTO.TURKPOS_RETVAL_Dekont_ID;
            kyt.TURKPOS_RETVAL_Ext_Data = pos_OdemePostDTO.TURKPOS_RETVAL_Ext_Data;
            kyt.TURKPOS_RETVAL_GUID = pos_OdemePostDTO.TURKPOS_RETVAL_GUID;
            kyt.TURKPOS_RETVAL_Hash = pos_OdemePostDTO.TURKPOS_RETVAL_Hash;
            kyt.TURKPOS_RETVAL_Islem_ID= pos_OdemePostDTO.TURKPOS_RETVAL_Islem_ID;
            kyt.TURKPOS_RETVAL_Islem_Tarih = Convert.ToDateTime(pos_OdemePostDTO.TURKPOS_RETVAL_Islem_Tarih);
            kyt.TURKPOS_RETVAL_Odeme_Tutari = Convert.ToDecimal(pos_OdemePostDTO.TURKPOS_RETVAL_Odeme_Tutari);
            kyt.TURKPOS_RETVAL_Sonuc = pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc;
            kyt.TURKPOS_RETVAL_Sonuc_Str = pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str;
            kyt.TURKPOS_RETVAL_Tahsilat_Tutari = Convert.ToDecimal(pos_OdemePostDTO.TURKPOS_RETVAL_Tahsilat_Tutari);
            kyt.TURKPOS_RETVAL_SiparisID = pos_OdemePostDTO.TURKPOS_RETVAL_Siparis_ID;

            db.TBL_POS_ISLEMs.Update(kyt);
            db.SaveChanges();

            // Faturaların durumunu "TAHSİL EDİLDİ" olarak güncelle
            if (!string.IsNullOrEmpty(kyt.TURKPOS_RETVAL_SiparisID))
            {
                try
                {
                    var invoiceNumbers = kyt.TURKPOS_RETVAL_SiparisID.Split(',')
                                            .Select(s => s.Trim())
                                            .Where(s => !string.IsNullOrEmpty(s))
                                            .ToList();

                    if (invoiceNumbers.Any())
                    {
                        // Safe parameterized SQL using IN clause is tricky with EF Core ExecuteSqlRaw, 
                        // doing it safely via loop or string manipulation if controlled
                        foreach (var invNo in invoiceNumbers)
                        {
                            var sql = $@"
                                UPDATE [VeriOkumaDonusum].[dbo].[TBL_FINANS_FATURA] 
                                SET Durum = 'TAHSİL EDİLDİ',
                                    Bekleyen_Bakiye = 0
                                WHERE Fatura_No = @p0";
                            
                            db.Database.ExecuteSqlRaw(sql, invNo);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't stop the success page rendering
                    Console.WriteLine($"[Basarili] Fatura durumu güncellenirken hata oluştu: {ex.Message}");
                }
            }

            sonucmodel sm = new sonucmodel();

            sm.tutar = pos_OdemePostDTO.TURKPOS_RETVAL_Tahsilat_Tutari.ToString();
            sm.dekontid= pos_OdemePostDTO.TURKPOS_RETVAL_Dekont_ID;
            sm.islemtarihi = Convert.ToDateTime(pos_OdemePostDTO.TURKPOS_RETVAL_Islem_Tarih);
            sm.siparisid= pos_OdemePostDTO.TURKPOS_RETVAL_Siparis_ID;
            sm.aciklama = pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str;


            return View(sm);

        }





        [HttpGet]
        [HttpPost]
        public IActionResult Basarisiz([FromForm] ST_TP_Islem_Odeme sonucNonSecure, [FromForm] Pos_OdemePostDTO sonuc3DSecure)
        {
            var kyt = db.TBL_POS_ISLEMs.Where(i => i.LNGKOD == Convert.ToInt32(sonuc3DSecure.TURKPOS_RETVAL_Siparis_ID)).FirstOrDefault();

            kyt.TURKPOS_RETVAL_Banka_Sonuc_kod = sonuc3DSecure.TURKPOS_RETVAL_Banka_Sonuc_Kod;
            kyt.TURKPOS_RETVAL_Dekont_ID = sonuc3DSecure.TURKPOS_RETVAL_Dekont_ID;
            kyt.TURKPOS_RETVAL_Ext_Data = sonuc3DSecure.TURKPOS_RETVAL_Ext_Data;
            kyt.TURKPOS_RETVAL_GUID = sonuc3DSecure.TURKPOS_RETVAL_GUID;
            kyt.TURKPOS_RETVAL_Hash = sonuc3DSecure.TURKPOS_RETVAL_Hash;
            kyt.TURKPOS_RETVAL_Islem_ID = sonuc3DSecure.TURKPOS_RETVAL_Islem_ID;
            kyt.TURKPOS_RETVAL_Islem_Tarih = Convert.ToDateTime(sonuc3DSecure.TURKPOS_RETVAL_Islem_Tarih);
            kyt.TURKPOS_RETVAL_Odeme_Tutari = Convert.ToDecimal(sonuc3DSecure.TURKPOS_RETVAL_Odeme_Tutari);
            kyt.TURKPOS_RETVAL_Sonuc = sonuc3DSecure.TURKPOS_RETVAL_Sonuc;
            kyt.TURKPOS_RETVAL_Sonuc_Str = sonuc3DSecure.TURKPOS_RETVAL_Sonuc_Str;
            kyt.TURKPOS_RETVAL_Tahsilat_Tutari = Convert.ToDecimal(sonuc3DSecure.TURKPOS_RETVAL_Tahsilat_Tutari);
            kyt.TURKPOS_RETVAL_SiparisID = sonuc3DSecure.TURKPOS_RETVAL_Siparis_ID;

            db.TBL_POS_ISLEMs.Update(kyt);
            db.SaveChanges();

            sonucmodel sm = new sonucmodel();

            sm.tutar = sonuc3DSecure.TURKPOS_RETVAL_Tahsilat_Tutari.ToString();
            sm.dekontid = sonuc3DSecure.TURKPOS_RETVAL_Dekont_ID;
            sm.islemtarihi = Convert.ToDateTime(sonuc3DSecure.TURKPOS_RETVAL_Islem_Tarih);
            sm.siparisid = sonuc3DSecure.TURKPOS_RETVAL_Siparis_ID;
            sm.aciklama = sonuc3DSecure.TURKPOS_RETVAL_Sonuc_Str;

            return View(sm);
        }



        //[HttpPost]
        //public async Task<ActionResult> KampanyaOdemeAsync(InfoCard kartbilgi)
        //{




        //    var binding = new BasicHttpBinding();
        //    var endpoint = new EndpointAddress("https://testposws.param.com.tr/turkpos.ws/service_turkpos_prod.asmx");

        //    TurkPosServisi client;


        //    // Örnek bir çağrı
        //    var result = await client.Pos_OdemeAsync(g);




        //    DateTime bugun = DateTime.Now;
        //    kartbilgi.guvenlik3D = true;
        //    string client_code = db.TBLPARAMETREs.Where(x => x.TXTPARAMETRE == "CLIENT_CODE" && x.BYTGRUP == 1000).Select(i => i.TXTDEGER).FirstOrDefault();
        //    string client_username = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "CLIENT_USERNAME" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
        //    string client_password = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "CLIENT_PASSWORD" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
        //    string guidsi = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "GUID" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
        //    string basarili_url = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "BASARILI_URL" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
        //    string basarisiz_url = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "BASARISIZ_URL" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
        //    string istek_url = db.TBLPARAMETREs.Where(y => y.TXTPARAMETRE == "ISTEK_URL" && y.BYTGRUP == 1000).Select(z => z.TXTDEGER).FirstOrDefault();
        //    string kdv = db.TBLPARAMETREs.Where(x => x.TXTPARAMETRE == "KDVORANI").Select(i => i.TXTDEGER).FirstOrDefault();
        //    decimal kdv_orani = Convert.ToDecimal(kdv);
        //    //asagıdaki string alanların kontrolü eklenecek

        //    string kk_adsoyad = kartbilgi.adsoyad;
        //    string kk_kartno = kartbilgi.kartno;
        //    string kk_sonay = kartbilgi.ay.ToString();
        //    string kk_sonyil = kartbilgi.yil.ToString();
        //    string kk_cvc = kartbilgi.guvenlikkodu;
        //    string kk_telno = kartbilgi.telefon;
        //    string kk_vergino = kartbilgi.vergino;
        //    string kk_unidoxno = kartbilgi.unidoxno;
        //    string kk_aciklama = kartbilgi.kampanya.aciklama;
        //    string siparis_ID = "";
        //    string siparis_aciklama = kartbilgi.kampanya.kampanyaadi;
        //    string kk_mailadresi = kartbilgi.email;
        //    int taksit = 1;
        //    string kk_securetype = "";
        //    if (kartbilgi.guvenlik3D)
        //    {
        //        kk_securetype = "3D";
        //    }
        //    else
        //    {
        //        kk_securetype = "NS";

        //    }

        //    //kontroller eklencek
        //    string onay1 = "";
        //    if (kartbilgi.onay1)
        //    {
        //        onay1 = "onaylandi";
        //    }
        //    else
        //    {
        //        onay1 = "red";
        //    }

        //    string onay2 = "";
        //    if (kartbilgi.onay2)
        //    {
        //        onay2 = "onaylandi";
        //    }
        //    else
        //    {
        //        onay2 = "red";
        //    }





        //    // kontrol eklenecek

        //    string toplamtutar = String.Format("{0:0.00;-0.00;0}", "10");

        //    decimal islem_tutar = Convert.ToDecimal(toplamtutar);

        //    int kartuzunluk = kk_kartno.Length;
        //    if (kartuzunluk != 16)
        //    {
        //        return RedirectToAction("Hata", "Kart", new { kod = "-1", mesaj = "Kart numarası eksik veya fazla karakter" });


        //    }


        //    string maskkart = kk_kartno.Substring(0, 8) + "******" + kk_kartno.Substring(14, 2);

        //    var sip_ID = db.SSP_POS_ISLEMLERI(1, maskkart, kk_adsoyad, kk_telno, kk_vergino, kk_unidoxno, islem_tutar, "", 0, "", "", bugun, "", "", "", "", "", "", bugun, islem_tutar, "", "", "", 0, bugun, bugun, "", bugun, "", kk_securetype, onay1, onay2, kartbilgi.kampanya.id, kartbilgi.kampanya.kampanyaadi, kk_mailadresi).FirstOrDefault();

        //    siparis_ID = sip_ID.ToString();



        //    ST_WS_Guvenlik gvn = new ST_WS_Guvenlik();
        //    gvn.CLIENT_CODE = client_code;
        //    gvn.CLIENT_USERNAME = client_username;
        //    gvn.CLIENT_PASSWORD = client_password;

        //    //CLIENT_CODE & GUID & Taksit & Islem_Tutar & Toplam_Tutar & Siparis_ID & Hata_URL & Basarili_URL



        //    TurkPosWSPROD prodser = new TurkPosWSPROD();
        //    string hashkod = "";
        //    try
        //    {
        //        string data = client_code + guidsi + taksit.ToString() + islemtutar + toplamtutar + siparis_ID + basarisiz_url + basarili_url;
        //        hashkod = prodser.SHA2B64(data);
        //    }
        //    catch (Exception ex)
        //    {
        //        string mesaj = "Hash Kod Alınırken Hata Alındı Mesaj : " + ex.Message;


        //        var d = db.SSP_POS_ISLEMLERI(0, maskkart, kk_adsoyad, kk_telno, kk_vergino, kk_unidoxno, islem_tutar, "", 0, "", mesaj, bugun, "", "", "", "", "", "", bugun, islem_tutar, siparis_ID, "", "", 0, bugun, bugun, "", bugun, mesaj, kk_securetype, onay1, onay2, 0, "", "").FirstOrDefault();


        //        string hatakodu = ex.Message;
        //        string hatamesaji = "Sistemde bir hata oluştu daha sonra tekrar deneyebilirsiniz.!!!!";

        //        return RedirectToAction("Hata", "Kart", new { kod = hatakodu, mesaj = hatamesaji });

        //    }

        //    //kontrol eklenecek


        //    try
        //    {

        //        var sonuc = prodser.Pos_Odeme(gvn, guidsi, kk_adsoyad, kk_kartno, kk_sonay, kk_sonyil,
        //                                     kk_cvc, kk_telno, basarisiz_url, basarili_url, siparis_ID,
        //                                      kk_aciklama, taksit, islemtutar, toplamtutar, hashkod, kk_securetype,
        //                                         "2", "127.0.0.1", "", "", "", "", "", "", "", "", "", "", "");

        //        sip_ID = db.SSP_POS_ISLEMLERI(2, maskkart, kk_adsoyad, kk_telno, kk_vergino, kk_unidoxno, islem_tutar, sonuc.Islem_ID.ToString(), Convert.ToDecimal(sonuc.Komisyon_Oran), sonuc.Sonuc, sonuc.Sonuc_Str, bugun, "", "", "", "", "", "", bugun, islem_tutar, siparis_ID, "", "", 0, bugun, bugun, "", bugun, "", kk_securetype, onay1, onay2, 0, "", "").FirstOrDefault();


        //        if (sonuc.Sonuc == "1" && sonuc.Sonuc_Str == "İşlem Başarılı" && kk_securetype == "3D")
        //        {
        //            string url = sonuc.UCD_URL;

        //            string jsonG = System.Text.Json.JsonSerializer.Serialize(new PosOdemeCookieModel() { ST_TP_Islem_Odeme = sonuc });
        //            HttpCookie cook = new HttpCookie("PosOdemeCookie", jsonG);
        //            cook.Expires = DateTime.Now.AddMinutes(3);
        //            Response.Cookies.Add(cook);

        //            //kontrol ekle time out
        //            return Redirect(url);
        //        }


        //        if (sonuc.Sonuc == "1" && sonuc.Sonuc_Str == "İşlem Başarılı" && kk_securetype == "NS")
        //        {
        //            TempData["sonuc"] = sonuc;
        //            return RedirectToAction("BasariliOdemeNS", "Kart");
        //        }

        //        if (sonuc.Sonuc != "1" && sonuc.Sonuc_Str != "İşlem Başarılı")
        //        {
        //            TempData["sonuc"] = sonuc;
        //            //   return RedirectToAction("Hata", "Kart", new { kod = 1, mesaj = "TEST" });
        //            return RedirectToAction("BasarisizOdemeNS", "Kart");
        //        }






        //    }
        //    catch (Exception)
        //    {

        //        throw;
        //    }



        //    return RedirectToAction("Index", "Home");

        //}


        //public ActionResult BasariliOdeme(Pos_OdemePostDTO pos_OdemePostDTO)
        //{
        //    if (pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str is null || pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str == null)
        //    {
        //        return RedirectToAction("Index", "Home");
        //    }

        //    DateTime bugun = DateTime.Now;
        //    //3 tipi 3d secureden sonra islem sonucu için
        //    var sip_ID = db.SSP_POS_ISLEMLERI(3, "", "", "", "", "", 0, "", 0, "", "", bugun, pos_OdemePostDTO.TURKPOS_RETVAL_Banka_Sonuc_Kod, pos_OdemePostDTO.TURKPOS_RETVAL_Dekont_ID, pos_OdemePostDTO.TURKPOS_RETVAL_Ext_Data, pos_OdemePostDTO.TURKPOS_RETVAL_GUID, pos_OdemePostDTO.TURKPOS_RETVAL_Hash, pos_OdemePostDTO.TURKPOS_RETVAL_Islem_ID, DateTime.Parse(pos_OdemePostDTO.TURKPOS_RETVAL_Islem_Tarih), Convert.ToDecimal(pos_OdemePostDTO.TURKPOS_RETVAL_Odeme_Tutari), pos_OdemePostDTO.TURKPOS_RETVAL_Siparis_ID, pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc, pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str, Convert.ToDecimal(pos_OdemePostDTO.TURKPOS_RETVAL_Tahsilat_Tutari), bugun, bugun, "", bugun, pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str, "", "", "", 0, "", "").FirstOrDefault();


        //    MailGonder mg = new MailGonder();
        //    mg.gonder(pos_OdemePostDTO.TURKPOS_RETVAL_Dekont_ID);


        //    ViewBag.Sonuc = pos_OdemePostDTO;
        //    return View();
        //}

        //public ActionResult BasarisizOdeme(ST_TP_Islem_Odeme sonucNonSecure, Pos_OdemePostDTO pos_OdemePostDTO)
        //{

        //    if (pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str is null || pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str == null)
        //    {
        //        return RedirectToAction("Index", "Home");
        //    }
        //    DateTime bugun = DateTime.Now;
        //    //3 tipi 3d secureden sonra islem sonucu için
        //    var sip_ID = db.SSP_POS_ISLEMLERI(3, "", "", "", "", "", 0, "", 0, "", "", bugun, pos_OdemePostDTO.TURKPOS_RETVAL_Banka_Sonuc_Kod, pos_OdemePostDTO.TURKPOS_RETVAL_Dekont_ID, pos_OdemePostDTO.TURKPOS_RETVAL_Ext_Data, pos_OdemePostDTO.TURKPOS_RETVAL_GUID, pos_OdemePostDTO.TURKPOS_RETVAL_Hash, pos_OdemePostDTO.TURKPOS_RETVAL_Islem_ID, DateTime.Parse(pos_OdemePostDTO.TURKPOS_RETVAL_Islem_Tarih), Convert.ToDecimal(pos_OdemePostDTO.TURKPOS_RETVAL_Odeme_Tutari), pos_OdemePostDTO.TURKPOS_RETVAL_Siparis_ID, pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc, pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str, Convert.ToDecimal(pos_OdemePostDTO.TURKPOS_RETVAL_Tahsilat_Tutari), bugun, bugun, "", bugun, pos_OdemePostDTO.TURKPOS_RETVAL_Sonuc_Str, "", "", "", 0, "", "").FirstOrDefault();


        //    ViewBag.Sonuc = pos_OdemePostDTO;
        //    return View();
        //}
        //public ActionResult BasariliOdemeNS(ST_TP_Islem_Odeme sonucNonSecure)
        //{
        //    var model = (ST_TP_Islem_Odeme)TempData["sonuc"];

        //    if (model.Sonuc_Str is null || model.Sonuc_Str == null)
        //    {
        //        return RedirectToAction("Index", "Home");
        //    }
        //    ViewBag.Sonuc = model;

        //    return View();
        //}
        //public ActionResult BasarisizOdemeNS(ST_TP_Islem_Odeme sonucNonSecure)
        //{
        //    var model = (ST_TP_Islem_Odeme)TempData["sonuc"];

        //    if (model.Sonuc_Str is null || model.Sonuc_Str == null)
        //    {
        //        return RedirectToAction("Index", "Home");
        //    }

        //    //  var model = TempData["sonuc"];
        //    ViewBag.Sonuc = model;

        //    return View();
        //}
        //public ActionResult Hata(string kod, string mesaj)
        //{



        //    ViewBag.Kod = kod;
        //    ViewBag.Mesaj = mesaj;

        //    return View();
        //}
    }
}
