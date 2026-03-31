$smtp = New-Object System.Net.Mail.SmtpClient('smtp.gmail.com', 587)
$smtp.EnableSsl = $true
$smtp.Credentials = New-Object System.Net.NetworkCredential('bilgilendirme@univera.com.tr', 'ssfkhdovnskzexvo')
$smtp.Timeout = 15000

$msg = New-Object System.Net.Mail.MailMessage
$msg.From = 'bilgilendirme@univera.com.tr'
$msg.To.Add('melih.bulut@univera.com.tr')
$msg.Subject = 'SOS - AI Destekli Gelistirme Is Akisi Detaylari'
$msg.IsBodyHtml = $true
$msg.Body = @"
<div style="font-family: Inter, -apple-system, sans-serif; max-width: 600px; margin: 0 auto; padding: 40px 20px; color: #1d1d1f;">

    <div style="text-align: center; margin-bottom: 40px;">
        <div style="width: 56px; height: 56px; background: #1d1d1f; border-radius: 14px; display: inline-flex; align-items: center; justify-content: center;">
            <span style="color: white; font-size: 22px; font-weight: 700;">S</span>
        </div>
        <h1 style="font-size: 26px; font-weight: 700; margin: 20px 0 4px 0;">AI Destekli Gelistirme Is Akisi</h1>
        <p style="font-size: 14px; color: #86868b; margin: 0;">Sales Operating System - Claude Code Entegrasyonu</p>
    </div>

    <div style="margin-bottom: 32px;">
        <p style="font-size: 15px; color: #424245; line-height: 1.7;">
            SOS projesinde gelistirme surecini hizlandirmak ve hata oranini minimuma indirmek icin Claude Code (AI) tabanli otonom bir is akisi kuruldu. Asagida bu akisin tum adimlari detayli olarak anlatilmaktadir.
        </p>
    </div>

    <div style="background: #f5f5f7; border-radius: 16px; padding: 24px; margin-bottom: 16px;">
        <div style="margin-bottom: 12px;">
            <span style="display: inline-block; width: 32px; height: 32px; background: #1d1d1f; border-radius: 8px; color: white; font-size: 14px; font-weight: 700; text-align: center; line-height: 32px; margin-right: 8px;">1</span>
            <strong style="font-size: 16px;">Gorev Teslim Alma</strong>
        </div>
        <p style="font-size: 14px; color: #424245; line-height: 1.7; margin: 0;">
            Gelistirici gorevi iletir (ornegin: "Login sayfasindaki hero card'i ortala"). Claude Code gorevi analiz eder, ilgili dosyalari okur ve yapilmasi gerekenleri belirler. Kullanicidan ek onay istemeden dogrudan isleme baslar.
        </p>
    </div>

    <div style="background: #f5f5f7; border-radius: 16px; padding: 24px; margin-bottom: 16px;">
        <div style="margin-bottom: 12px;">
            <span style="display: inline-block; width: 32px; height: 32px; background: #1d1d1f; border-radius: 8px; color: white; font-size: 14px; font-weight: 700; text-align: center; line-height: 32px; margin-right: 8px;">2</span>
            <strong style="font-size: 16px;">Paralel Agent Sistemi ile Kodlama</strong>
        </div>
        <p style="font-size: 14px; color: #424245; line-height: 1.7; margin: 0;">
            Birden fazla is varsa (ornegin favicon degisikligi + interaktif efekt), her biri bagimsiz bir <strong>Agent</strong>'a delege edilir. Agent'lar paralel calisir, boylece 2 is ayni anda tamamlanir. Her agent kendi dosyasini okur, analiz eder ve degisikligi uygular.
        </p>
    </div>

    <div style="background: #f5f5f7; border-radius: 16px; padding: 24px; margin-bottom: 16px;">
        <div style="margin-bottom: 12px;">
            <span style="display: inline-block; width: 32px; height: 32px; background: #4f46e5; border-radius: 8px; color: white; font-size: 14px; font-weight: 700; text-align: center; line-height: 32px; margin-right: 8px;">3</span>
            <strong style="font-size: 16px;">Runtime Dogrulama (Curl Testi)</strong>
        </div>
        <p style="font-size: 14px; color: #424245; line-height: 1.7; margin: 0;">
            Kod degisikligi yapildiktan sonra sadece "build basarili" demek yetmez. <strong>curl</strong> komutuyla calisan uygulamaya HTTP istegi atilir ve donen HTML ciktisinda degisikligin gercekten yer alip almadigi kontrol edilir. Ornegin yeni eklenen bir CSS class'i, bir JavaScript fonksiyonu veya degisen bir metin icerigin HTML'de gorunup gorunmedigi dogrulanir. Dogrulama basarisiz olursa hata duzeltilir ve tekrar test edilir.
        </p>
    </div>

    <div style="background: #f5f5f7; border-radius: 16px; padding: 24px; margin-bottom: 16px;">
        <div style="margin-bottom: 12px;">
            <span style="display: inline-block; width: 32px; height: 32px; background: #1d1d1f; border-radius: 8px; color: white; font-size: 14px; font-weight: 700; text-align: center; line-height: 32px; margin-right: 8px;">4</span>
            <strong style="font-size: 16px;">Tamamlama Bildirimi</strong>
        </div>
        <p style="font-size: 14px; color: #424245; line-height: 1.7; margin: 0;">
            Tum dogrulamalar gectiginde ekrana <strong>"Islem tamam King."</strong> yazilir. Bu, gelistiriciye isin bittigini aninda bildirir.
        </p>
    </div>

    <div style="background: #eef2ff; border-radius: 16px; padding: 24px; margin-bottom: 16px;">
        <div style="margin-bottom: 12px;">
            <span style="display: inline-block; width: 32px; height: 32px; background: #4f46e5; border-radius: 8px; color: white; font-size: 14px; font-weight: 700; text-align: center; line-height: 32px; margin-right: 8px;">5</span>
            <strong style="font-size: 16px;">Akilli Bildirim Sistemi (Idle Notifier)</strong>
        </div>
        <p style="font-size: 14px; color: #424245; line-height: 1.7; margin: 0;">
            Is tamamlandiginda arka planda bir PowerShell scripti (idle-notifier.ps1) baslatilir. Bu script Windows API uzerinden fare ve klavye hareketlerini izler. Eger gelistirici <strong>30 saniye boyunca</strong> ekrana dokunmazsa (yani baska bir isle mesgulse veya masasindan kalkmissa), otomatik olarak e-posta bildirimi gonderilir. Boylece gelistirici ekran basinda beklemek zorunda kalmaz.
        </p>
    </div>

    <div style="background: #f5f5f7; border-radius: 16px; padding: 24px; margin-bottom: 16px;">
        <div style="margin-bottom: 12px;">
            <span style="display: inline-block; width: 32px; height: 32px; background: #1d1d1f; border-radius: 8px; color: white; font-size: 14px; font-weight: 700; text-align: center; line-height: 32px; margin-right: 8px;">6</span>
            <strong style="font-size: 16px;">Kalici Hafiza</strong>
        </div>
        <p style="font-size: 14px; color: #424245; line-height: 1.7; margin: 0;">
            Claude Code, konusmalar arasi kalici bir hafiza sistemine sahiptir. Gelistiricinin tercihleri, SMTP ayarlari, proje detaylari ve is akisi kurallari hafizada saklanir. Yeni bir oturum acildiginda bile onceki talimatlar hatirlanir. Boylece her seferinde ayni seyleri tekrar soylemek gerekmez.
        </p>
    </div>

    <div style="background: #1d1d1f; border-radius: 16px; padding: 28px; margin: 32px 0; text-align: center;">
        <p style="font-size: 15px; color: #ffffff; line-height: 1.8; margin: 0;">
            <strong>Ozetle:</strong> Gorev ver &#8594; Agent kodlar &#8594; Curl dogrular &#8594; "Islem tamam King." &#8594; 30 sn idle &#8594; Mail gelir
        </p>
    </div>

    <p style="font-size: 16px; font-weight: 600; color: #4f46e5; text-align: center; margin: 32px 0;">
        Babalar sozunu tutar.
    </p>

    <div style="text-align: center; padding-top: 24px; border-top: 1px solid #e5e5e5;">
        <span style="font-size: 11px; color: #86868b; letter-spacing: 2px;">UNIVERA &#8226; SALES OPERATING SYSTEM</span>
    </div>
</div>
"@

try {
    $smtp.Send($msg)
    Write-Host "Mail gonderildi"
} catch {
    Write-Host "Hata: $($_.Exception.Message)"
}
$msg.Dispose()
$smtp.Dispose()
