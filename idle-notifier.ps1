# SOS Idle Notifier
# Fare/klavye hareketsizligini izler, 60 saniye idle olursa mail atar
# Kullanim: powershell -ExecutionPolicy Bypass -File idle-notifier.ps1

param(
    [int]$IdleSeconds = 30,
    [string]$ToEmail = "egemen.baskan@univera.com.tr",
    [string]$SmtpHost = "smtp.gmail.com",
    [int]$SmtpPort = 587,
    [string]$SmtpUser = "bilgilendirme@univera.com.tr",
    [string]$SmtpPass = "ssfkhdovnskzexvo"
)

# Windows API - GetLastInputInfo
Add-Type @"
using System;
using System.Runtime.InteropServices;

public struct LASTINPUTINFO {
    public uint cbSize;
    public uint dwTime;
}

public class IdleDetector {
    [DllImport("user32.dll")]
    static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public static uint GetIdleTime() {
        LASTINPUTINFO lii = new LASTINPUTINFO();
        lii.cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO));
        GetLastInputInfo(ref lii);
        return ((uint)Environment.TickCount - lii.dwTime);
    }
}
"@

function Send-NotificationEmail {
    try {
        $smtpClient = New-Object System.Net.Mail.SmtpClient($SmtpHost, $SmtpPort)
        $smtpClient.EnableSsl = $true
        $smtpClient.Credentials = New-Object System.Net.NetworkCredential($SmtpUser, $SmtpPass)

        $mailMessage = New-Object System.Net.Mail.MailMessage
        $mailMessage.From = $SmtpUser
        $mailMessage.To.Add($ToEmail)
        $mailMessage.Subject = "SOS - Gorevler tamamlandi"
        $mailMessage.IsBodyHtml = $true
        $mailMessage.Body = @"
<div style="font-family: Inter, -apple-system, sans-serif; max-width: 480px; margin: 0 auto; padding: 40px 20px;">
    <div style="text-align: center; margin-bottom: 32px;">
        <div style="width: 48px; height: 48px; background: #1d1d1f; border-radius: 12px; display: inline-flex; align-items: center; justify-content: center;">
            <span style="color: white; font-size: 18px; font-weight: 700;">S</span>
        </div>
    </div>
    <h2 style="font-size: 22px; font-weight: 600; color: #1d1d1f; text-align: center; margin-bottom: 8px;">
        Islem tamam King.
    </h2>
    <p style="font-size: 15px; color: #86868b; text-align: center; line-height: 1.6;">
        Tum gorevler basariyla tamamlandi. 30 saniyedir herhangi bir aktivite algilanmadi.
    </p>
    <div style="text-align: center; margin-top: 24px; padding-top: 24px; border-top: 1px solid #f5f5f7;">
        <span style="font-size: 11px; color: #86868b; letter-spacing: 2px;">SALES OPERATING SYSTEM</span>
    </div>
</div>
"@

        $smtpClient.Send($mailMessage)
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Mail gonderildi: $ToEmail" -ForegroundColor Green

        $mailMessage.Dispose()
        $smtpClient.Dispose()
        return $true
    }
    catch {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Mail gonderilemedi: $_" -ForegroundColor Red
        return $false
    }
}

# Ana dongu
Write-Host ""
Write-Host "  SOS Idle Notifier baslatildi" -ForegroundColor Cyan
Write-Host "  Hedef: $ToEmail" -ForegroundColor Gray
Write-Host "  Bekleme: $IdleSeconds saniye hareketsizlik" -ForegroundColor Gray
Write-Host "  Durdurmak icin: Ctrl+C" -ForegroundColor Gray
Write-Host ""

$mailSent = $false

while ($true) {
    $idleMs = [IdleDetector]::GetIdleTime()
    $idleSec = [math]::Floor($idleMs / 1000)

    if ($idleSec -ge $IdleSeconds -and -not $mailSent) {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] $IdleSeconds saniye hareketsizlik algilandi, mail gonderiliyor..." -ForegroundColor Yellow
        $mailSent = Send-NotificationEmail
    }

    # Mail gonderildiyse isini bitir ve kapat
    if ($mailSent) {
        Write-Host "[$(Get-Date -Format 'HH:mm:ss')] Mail gonderildi, kapatiliyor." -ForegroundColor Cyan
        Start-Sleep -Seconds 2
        exit 0
    }

    Start-Sleep -Seconds 2
}
