using Microsoft.EntityFrameworkCore;
using UniCP.Models.MsK.SpModels;

namespace UniCP.DbData
{
    public partial class MskDbContext
    {
        public List<Sp_Deneme> GetDeneme(int p1, string p2)
        {
            return Set<Sp_Deneme>()
                .FromSqlRaw("EXEC DENEME @P1 = @p0, @P2 = @p1", p1, p2)
                .AsNoTracking()
                .ToList();
        }
        public List<Fn_Deneme> Fon_Deneme(int p)
        {
            return Set<Fn_Deneme>()
               .FromSqlRaw("SELECT * FROM dbo.FN_DENEME({0})", p)
               .AsNoTracking()
               .ToList();
        }

        public List<SSP_N4B_TICKETLARI> SP_N4B_TICKETLARI(int ORTAKPROJEKOD, string EMAIL ,int BILDIRIMTIP)
        {
            return Set<SSP_N4B_TICKETLARI>()
                .FromSqlRaw("EXEC SSP_N4B_TICKETLARI @ORTAKPROJEKOD = @P0, @EMAIL = @P1,@BILDIRIMTIP = @P2", ORTAKPROJEKOD, EMAIL, BILDIRIMTIP)
                .AsNoTracking()
                .ToList();
        }
        public List<SSP_N4B_TICKET_DURUM_SAYILARI> SP_N4B_TICKET_DURUM_SAYILARI(int ORTAKPROJEKOD, string EMAIL,DateTime TARIH )
        {
            return Set<SSP_N4B_TICKET_DURUM_SAYILARI>()
                .FromSqlRaw("EXEC SSP_N4B_TICKET_DURUM_SAYILARI @ORTAKPROJEKOD = @P0,@EMAIL = @P1,@TARIH = @p2", ORTAKPROJEKOD, EMAIL , TARIH)
                .AsNoTracking()
                .ToList();
        }

        public List<SSP_N4B_SLA_ORAN> SP_N4B_SLA_ORAN(int ORTAKPROJEKOD)
        {
            return Set<SSP_N4B_SLA_ORAN>()
                .FromSqlRaw("EXEC SSP_N4B_SLA_ORAN @ORTAKPROJEKOD = @P0", ORTAKPROJEKOD)
                .AsNoTracking()
                .ToList();
        }

        public List<SSP_TFS_GELISTIRME> SP_TFS_GELISTIRME(int LNGORTAKFIRMAKOD)
        {
            return Set<SSP_TFS_GELISTIRME>()
                .FromSqlRaw("EXEC SSP_TFS_GELISTIRME @ORTAKPROJEKOD = @P0", LNGORTAKFIRMAKOD)
                .AsNoTracking()
                .ToList();
        }

        public List<SpVarunaSiparisResult> SP_VARUNA_SIPARIS(int LNGORTAKFIRMAKOD)
        {
            return Set<SpVarunaSiparisResult>()
                .FromSqlRaw("EXEC SSP_VARUNA_SIPARIS @ORTAKPROJEKOD = @P0", LNGORTAKFIRMAKOD)
                .AsNoTracking()
                .ToList();
        }
        public List<SSP_VARUNA_SIPARIS_DETAY> SP_VARUNA_SIPARIS_DETAY(string ORDERID)
        {
            return Set<SSP_VARUNA_SIPARIS_DETAY>()
                .FromSqlRaw("EXEC SSP_VARUNA_SIPARIS_DETAY @ORDERID = @P0", ORDERID)
                .AsNoTracking()
                .ToList();
        }
         public List<SSP_VARUNA_CHART_DATA> SP_VARUNA_CHART_DATA(int LNGORTAKFIRMAKOD)
        {
            return Set<SSP_VARUNA_CHART_DATA>()
                .FromSqlRaw("EXEC SSP_VARUNA_CHART_DATA @ORTAKPROJEKOD = @P0", LNGORTAKFIRMAKOD)
                .AsNoTracking()
                .ToList();
        }


        // Async Versions
        public Task<List<SSP_N4B_TICKETLARI>> SP_N4B_TICKETLARIAsync(int ORTAKPROJEKOD, string EMAIL, int BILDIRIMTIP)
        {
            return Set<SSP_N4B_TICKETLARI>()
                .FromSqlRaw("EXEC SSP_N4B_TICKETLARI @ORTAKPROJEKOD = @P0, @EMAIL = @P1,@BILDIRIMTIP = @P2", ORTAKPROJEKOD, EMAIL, BILDIRIMTIP)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_N4B_TICKET_DURUM_SAYILARI>> SP_N4B_TICKET_DURUM_SAYILARIAsync(int ORTAKPROJEKOD, string EMAIL, DateTime TARIH)
        {
            return Set<SSP_N4B_TICKET_DURUM_SAYILARI>()
                .FromSqlRaw("EXEC SSP_N4B_TICKET_DURUM_SAYILARI @ORTAKPROJEKOD = @P0,@EMAIL = @P1,@TARIH = @p2", ORTAKPROJEKOD, EMAIL, TARIH)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_N4B_SLA_ORAN>> SP_N4B_SLA_ORANAsync(int ORTAKPROJEKOD)
        {
            return Set<SSP_N4B_SLA_ORAN>()
                .FromSqlRaw("EXEC SSP_N4B_SLA_ORAN @ORTAKPROJEKOD = @P0", ORTAKPROJEKOD)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_TFS_GELISTIRME>> SP_TFS_GELISTIRMEAsync(int LNGORTAKFIRMAKOD)
        {
            return Set<SSP_TFS_GELISTIRME>()
                .FromSqlRaw("EXEC SSP_TFS_GELISTIRME @ORTAKPROJEKOD = @P0", LNGORTAKFIRMAKOD)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SpVarunaSiparisResult>> SP_VARUNA_SIPARISAsync(int LNGORTAKFIRMAKOD)
        {
            return Set<SpVarunaSiparisResult>()
                .FromSqlRaw("EXEC SSP_VARUNA_SIPARIS @ORTAKPROJEKOD = @P0", LNGORTAKFIRMAKOD)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_VARUNA_SIPARIS_DETAY>> SP_VARUNA_SIPARIS_DETAYAsync(string ORDERID)
        {
            return Set<SSP_VARUNA_SIPARIS_DETAY>()
                .FromSqlRaw("EXEC SSP_VARUNA_SIPARIS_DETAY @ORDERID = @P0", ORDERID)
                .AsNoTracking()
                .ToListAsync();
        }
        
        public Task<List<SSP_VARUNA_CHART_DATA>> SP_VARUNA_CHART_DATAAsync(int LNGORTAKFIRMAKOD)
        {
            return Set<SSP_VARUNA_CHART_DATA>()
                .FromSqlRaw("EXEC SSP_VARUNA_CHART_DATA @ORTAKPROJEKOD = @P0", LNGORTAKFIRMAKOD)
                .AsNoTracking()
                .ToListAsync();
        }
        // Batch SP Methods
        // Batch SP Methods
        public Task<List<SSP_VARUNA_SIPARIS_COKLU>> SP_VARUNA_SIPARIS_COKLU_FILTREAsync(string ORTAKPROJEKODLAR)
        {
            return Set<SSP_VARUNA_SIPARIS_COKLU>()
                .FromSqlRaw("EXEC [dbo].[SSP_VARUNA_SIPARIS_COKLU_FILTRE] @ORTAKPROJEKODLAR = {0}", ORTAKPROJEKODLAR)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_VARUNA_SIPARIS_DETAY_COKLU>> SP_VARUNA_SIPARIS_DETAY_COKLU_FILTREAsync(string ORDERIDS)
        {
            return Set<SSP_VARUNA_SIPARIS_DETAY_COKLU>()
                .FromSqlRaw("EXEC [dbo].[SSP_VARUNA_SIPARIS_DETAY_COKLU_FILTRE] @ORDERIDS = {0}", ORDERIDS)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_VARUNA_CHART_DATA_COKLU>> SP_VARUNA_CHART_DATA_COKLU_FILTREAsync(string ORTAKPROJEKODLAR)
        {
            return Set<SSP_VARUNA_CHART_DATA_COKLU>()
                .FromSqlRaw("EXEC [dbo].[SSP_VARUNA_CHART_DATA_COKLU_FILTRE] @ORTAKPROJEKODLAR = {0}", ORTAKPROJEKODLAR)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_TFS_GELISTIRME_COKLU>> SP_TFS_GELISTIRME_COKLU_FILTREAsync(string ORTAKPROJEKODLAR)
        {
            return Set<SSP_TFS_GELISTIRME_COKLU>()
                .FromSqlRaw("EXEC [dbo].[SSP_TFS_GELISTIRME_COKLU_FILTRE] @ORTAKPROJEKODLAR = {0}", ORTAKPROJEKODLAR)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_N4B_TICKETLARI_COKLU>> SP_N4B_TICKETLARI_COKLU_FILTREAsync(string ORTAKPROJEKODLAR, string EMAIL, int BILDIRIMTIP)
        {
            // Note: User confirmed extra parameters are needed
            return Set<SSP_N4B_TICKETLARI_COKLU>()
                .FromSqlRaw("EXEC [dbo].[SSP_N4B_TICKETLARI_COKLU_FILTRE] @ORTAKPROJEKODLAR = {0}, @EMAIL = {1}, @BILDIRIMTIP = {2}", ORTAKPROJEKODLAR, EMAIL, BILDIRIMTIP)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_N4B_TICKET_DURUM_SAYILARI_COKLU>> SP_N4B_TICKET_DURUM_SAYILARI_COKLU_FILTREAsync(string ORTAKPROJEKODLAR, string EMAIL, DateTime TARIH)
        {
            return Set<SSP_N4B_TICKET_DURUM_SAYILARI_COKLU>()
                .FromSqlRaw("EXEC [dbo].[SSP_N4B_TICKET_DURUM_SAYILARI_COKLU_FILTRE] @ORTAKPROJEKODLAR = {0}, @EMAIL = {1}, @TARIH = {2}", ORTAKPROJEKODLAR, EMAIL, TARIH)
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_N4B_SLA_ORAN_COKLU>> SP_N4B_SLA_ORAN_COKLU_FILTREAsync(string ORTAKPROJEKODLAR)
        {
            return Set<SSP_N4B_SLA_ORAN_COKLU>()
                .FromSqlRaw("EXEC [dbo].[SSP_N4B_SLA_ORAN_COKLU_FILTRE] @ORTAKPROJEKODLAR = {0}", ORTAKPROJEKODLAR)
                .AsNoTracking()
                .ToListAsync();
        }

        // ---- GENEL SP Methods (No Parameters - All Companies) ----

        public Task<List<SSP_N4B_TICKET_DURUM_SAYILARI>> SP_N4B_TICKET_DURUM_SAYILARI_GENELAsync()
        {
            return Set<SSP_N4B_TICKET_DURUM_SAYILARI>()
                .FromSqlRaw("EXEC [dbo].[SSP_N4B_TICKET_DURUM_SAYILARI_GENEL]")
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_N4B_TICKETLARI>> SP_N4B_TICKETLARI_GENELAsync()
        {
            return Set<SSP_N4B_TICKETLARI>()
                .FromSqlRaw("EXEC [dbo].[SSP_N4B_TICKETLARI_GENEL]")
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_N4B_SLA_ORAN>> SP_N4B_SLA_ORAN_GENELAsync()
        {
            return Set<SSP_N4B_SLA_ORAN>()
                .FromSqlRaw("EXEC [dbo].[SSP_N4B_SLA_ORAN_GENEL]")
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SSP_TFS_GELISTIRME>> SP_TFS_GELISTIRME_GENELAsync()
        {
            return Set<SSP_TFS_GELISTIRME>()
                .FromSqlRaw("EXEC [dbo].[SSP_TFS_GELISTIRME_GENEL]")
                .AsNoTracking()
                .ToListAsync();
        }

        public Task<List<SpVarunaSiparisResult>> SP_VARUNA_SIPARIS_GENELAsync()
        {
            return Set<SpVarunaSiparisResult>()
                .FromSqlRaw("EXEC [dbo].[SSP_VARUNA_SIPARIS_GENEL]")
                .AsNoTracking()
                .ToListAsync();
        }
    }
}
