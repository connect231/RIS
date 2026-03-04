using Microsoft.EntityFrameworkCore;
using UniCP.Models.MsK.SpModels;

namespace UniCP.DbData
{
    public partial class MskDbContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Sp_Deneme>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);
            });

            modelBuilder.Entity<Fn_Deneme>(entity =>
            {
                entity.HasNoKey();
                entity.ToView(null);   // EF bu nesnenin tablo olmadığını anlasın
            });

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
        }
    }
}
