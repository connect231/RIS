using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using SOS.DbData;
using SOS.Models.Kullanici;

namespace SOS.Services
{
    public class CustomUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, AppRole>
    {
        private readonly MskDbContext _mskDb;

        public CustomUserClaimsPrincipalFactory(
            UserManager<AppUser> userManager,
            RoleManager<AppRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            MskDbContext mskDb)
            : base(userManager, roleManager, optionsAccessor)
        {
            _mskDb = mskDb;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            // Fetch extra info from TBL_KULLANICI using the Identity User ID
            var dbUser = await _mskDb.TBL_KULLANICIs
                .Where(x => x.LNGIDENTITYKOD == user.Id)
                .Select(x => new { x.TXTFIRMAADI, x.LNGKULLANICITIPI, x.LNGORTAKFIRMAKOD })
                .FirstOrDefaultAsync();

            if (dbUser != null)
            {
                if (!string.IsNullOrEmpty(dbUser.TXTFIRMAADI))
                {
                    identity.AddClaim(new System.Security.Claims.Claim("FirmaAdi", (string)dbUser.TXTFIRMAADI!));
                }

                // Add UserType Claim
                // Default to 0 if null
                string typeVal = (dbUser.LNGKULLANICITIPI ?? 0).ToString();
                identity.AddClaim(new System.Security.Claims.Claim("UserType", typeVal));

                // Add FirmaKod Claim (CRITICAL for TenantProvider)
                if (dbUser.LNGORTAKFIRMAKOD.HasValue)
                {
                    identity.AddClaim(new System.Security.Claims.Claim("FirmaKod", dbUser.LNGORTAKFIRMAKOD.Value.ToString()));
                }
            }

            return identity;
        }
    }
}

