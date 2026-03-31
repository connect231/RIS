using Microsoft.EntityFrameworkCore;
using SOS.Models.MsK;
using SOS.Models.MsK.SpModels;

namespace SOS.DbData
{
    public partial class MskDbContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SSP_N4B_TICKETLARI>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);
            });

            modelBuilder.Entity<SSP_N4B_TICKET_DURUM_SAYILARI>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);
            });
            modelBuilder.Entity<SSP_N4B_SLA_ORAN>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);
            });

            modelBuilder.Entity<SSP_TFS_GELISTIRME>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);
            });
            modelBuilder.Entity<SpVarunaSiparisResult>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);
            });
            modelBuilder.Entity<SSP_VARUNA_SIPARIS_DETAY>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);
            });
             modelBuilder.Entity<SSP_VARUNA_CHART_DATA>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);
            });
            // Batch Models
            modelBuilder.Entity<SSP_VARUNA_SIPARIS_COKLU>(e => { e.HasNoKey(); e.ToView(null); });
            modelBuilder.Entity<SSP_VARUNA_CHART_DATA_COKLU>(e => { e.HasNoKey(); e.ToView(null); });
            modelBuilder.Entity<SSP_TFS_GELISTIRME_COKLU>(e => { e.HasNoKey(); e.ToView(null); });
            modelBuilder.Entity<SSP_N4B_TICKETLARI_COKLU>(e => { e.HasNoKey(); e.ToView(null); });
            modelBuilder.Entity<SSP_N4B_TICKET_DURUM_SAYILARI_COKLU>(e => { e.HasNoKey(); e.ToView(null); });
            modelBuilder.Entity<SSP_N4B_SLA_ORAN_COKLU>(e => { e.HasNoKey(); e.ToView(null); });
            modelBuilder.Entity<SSP_VARUNA_SIPARIS_DETAY_COKLU>(e => { e.HasNoKey(); e.ToView(null); });

            // SQL View: VIEW_CP_EXCEL_FATURA
            modelBuilder.Entity<VIEW_CP_EXCEL_FATURA>(entity =>
            {
                entity.ToView("VIEW_CP_EXCEL_FATURA");
            });
        }
    }
}

