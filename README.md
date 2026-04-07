# SOS — Sales Operating System

Finansal cockpit dashboard uygulaması. ASP.NET Core MVC (.NET 10), Razor Views, Tailwind CSS, SQL Server.

## Geliştirme

```bash
# Bağımlılıklar (ilk kurulum)
npm install
dotnet restore

# Secrets (connection string, SMTP vs.)
dotnet user-secrets set "ConnectionStrings:MsKConnection" "<cs>"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<cs>"
dotnet user-secrets set "Email:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:Username" "<email>"
dotnet user-secrets set "Email:Password" "<password>"

# Çalıştır
dotnet run --project SOS.csproj          # http://localhost:5165
npm run build:css                        # Tailwind watch (ayrı terminal)
```

## Proje Yapısı

- **Backend**: `Controllers/`, `Services/`, `DbData/`, `Models/`
- **Frontend**: `Views/Cockpit/Index.cshtml` (ana dashboard), `Views/Shared/_Layout.cshtml`
- **Configuration**: `appsettings.json` + `appsettings.Development.json` (placeholder) + **user-secrets**

## Dokümantasyon

- **`CLAUDE.md`** — mimari, iş mantığı, finansal hesaplama kuralları
- **`AGENTS.md`** — operasyonel kurallar, yasaklar, agent kullanım rehberi
- **`TODO.md`** — erteleme / gelecekteki iyileştirmeler
- **`.claude/agents/`** — proje-özel subagent tanımları (dotnet-cockpit-engineer, finans-hesaplama-auditor, sql-ef-query-pro, razor-ui-polisher)
