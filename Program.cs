using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UniCP.DbData;
using UniCP.Models;
using UniCP.Models.Kullanici;
using UniCP.Models.MsK;
using UniCP.Services;
using UniCP.Models;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddTransient<IEmailService, SmtpEmailService>();
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddTransient<UniCP.Services.GeminiService>();
builder.Services.AddSingleton<UniCP.Services.AI.OllamaService>();
builder.Services.AddScoped<UniCP.Services.AI.AIService>();
builder.Services.AddHttpClient<UniCP.Services.ZabbixService>(); // Registered ZabbixService
builder.Services.AddScoped<ICompanyResolutionService, CompanyResolutionService>();
builder.Services.AddScoped<IDatabaseMigrationService, DatabaseMigrationService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantProvider, TenantProvider>();
builder.Services.AddSingleton<IUrlEncryptionService, UrlEncryptionService>();
builder.Services.AddScoped<ILogService, DbLogService>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<ParamPosService>();

// Performance Optimization: Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddDbContextPool<DataContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlServer(connectionString);
});
builder.Services.AddDbContext<MskDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MsKConnection")));


builder.Services.AddIdentity<AppUser, AppRole>(options => { 
    options.SignIn.RequireConfirmedEmail = true; 

}).AddEntityFrameworkStores<DataContext>().AddDefaultTokenProviders()
  .AddClaimsPrincipalFactory<UniCP.Services.CustomUserClaimsPrincipalFactory>();

builder.Services.Configure<IdentityOptions>(options =>
{
    // Strengthened password policy (Sprint 1.3)
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireDigit = true;

    options.User.RequireUniqueEmail = true;
    // options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyz0123456789";

    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(3);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
});



var app = builder.Build();

// Run custom database migrations at startup (Sprint 2.3)
using (var scope = app.Services.CreateScope())
{
    var migrationService = scope.ServiceProvider.GetRequiredService<IDatabaseMigrationService>();
    await migrationService.ApplyCustomMigrationsAsync();
}

// Enforce Turkish Culture (tr-TR) for currency and date formatting
var cultureInfo = new System.Globalization.CultureInfo("tr-TR");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseResponseCompression(); // Optimized Placement
app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Basic Content Security Policy
app.Use(async (context, next) =>
{
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.tailwindcss.com https://cdn.jsdelivr.net https://unpkg.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net https://unpkg.com; font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net; img-src 'self' data: https:; connect-src 'self';";
    await next();
});

// app.MapStaticAssets();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 30 days
        const int durationInSeconds = 60 * 60 * 24 * 30;
        ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=" + durationInSeconds;
    }
});

app.MapControllerRoute(
    name: "urunler_by_kategori",
    pattern: "urunler/{url?}",
    defaults: new { controller = "Urun", action = "List" })
    .WithStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();



app.Run();
