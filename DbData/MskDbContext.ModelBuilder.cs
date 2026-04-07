using Microsoft.EntityFrameworkCore;
using SOS.Models.MsK;

namespace SOS.DbData
{
    public partial class MskDbContext
    {
        partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
        {
            // SQL View: VIEW_CP_EXCEL_FATURA
            modelBuilder.Entity<VIEW_CP_EXCEL_FATURA>(entity =>
            {
                entity.ToView("VIEW_CP_EXCEL_FATURA");
            });
        }
    }
}
