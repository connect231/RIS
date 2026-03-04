using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniCP.Models.Kullanici;

namespace UniCP.Models;

public class DataContext : IdentityDbContext<AppUser, AppRole, int>
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {

    }   

    public DbSet<UniCP.Models.AI.AIServiceLog> AIServiceLogs { get; set; }
}
