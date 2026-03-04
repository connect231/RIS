# Proje Değişiklik Logu (Changelog)

Bu dosya, *Univera Connect* projesi üzerinde yapılan tasarımsal ve kodsal değişiklikleri takip etmek amacıyla oluşturulmuştur.

## [09.01.2026]

### 🖥️ Login Ekranı (Login.cshtml)
- **Tasarım Denemeleri:** "Univera" isminin çok büyük (`text-8xl`), giriş kartının çok ince (`350px`) olduğu varyasyonlar denendi.
- **Birleşik Tasarım:** Logo ve giriş formunun tek bir cam kart içinde olduğu tasarım denendi.
- **✅ Geri Alma (Revert):** Kullanıcı geri bildirimi üzerine tasarım, en stabil ve beğenilen haline geri döndürüldü:
  - **Logo:** Minimalist 4 noktalı SVG (`110px`).
  - **Fontlar:** "Univera" (`text-7xl`), "Connect" (`text-3xl`).
  - **Kart:** `max-w-[380px]` genişliğinde, ortalanmış yapı.
  - **Yazılım:** Placeholder hatası (`@...`) giderildi.

### 🎫 Destek Talepleri (N4B/Index.cshtml)
- **Tablo Düzeni:** "Durum" kolonu ile "Firma" kolonu arasındaki boş/gereksiz kolon (Waiting Reason görseli için ayrılan) kaldırıldı.
- **Stil İyileştirmesi:** "Durum" rozetlerinin (`badge`) içindeki metinlerin alt satıra geçmesi engellendi (`whitespace-nowrap` eklendi). Bu sayede tablo satır yükseklikleri daha düzenli hale getirildi.

---
*Son Güncelleme: 09.01.2026 14:20*
