using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SOS.Models.Kullanici;

namespace SOS.Models;

public class DataContext : IdentityDbContext<AppUser, AppRole, int>
{
    public DataContext(DbContextOptions<DataContext> options) : base(options)
    {

    }   
}

