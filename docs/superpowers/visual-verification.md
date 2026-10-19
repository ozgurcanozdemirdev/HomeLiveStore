# Görsel revizyon kontrolleri — 11 Eylül 2026

## Değişiklik

Mağaza ve adminin ortak yerleşimi, tipografisi, renkleri ve bileşenleri yenilendi. Yuvarlak kartlar, gradyan zemin, büyük serif başlıklar ve eski Bootstrap kapsamlı CSS kaldırıldı. Beş ürün fotoğrafı ve ana sayfa fotoğrafı üretildi. Manrope fontu OFL lisansıyla yerel olarak eklendi. Veri modelindeki mevcut ürün görsel bağlantıları korunur; beş örnek SVG bağlantısı sunum katmanında yeni fotoğraflara eşlenir.

Sepet satır toplamı ve artı/eksi adet kontrolleri eklendi. Adet değiştirme satır sırasını ve klavye odağını korur. Boş sepetin sipariş düğmesi devre dışıdır. ProblemDetails hataları müşteriye okunabilir metin olarak gösterilir.

## Kanıt

- Çözüm derlemesi: sıfır hata, sıfır uyarı.
- `scripts/verify.ps1`: 3 alan kontrolü + 36 gerçek HTTP kontrolü geçti.
- `node --check HomeLiveStore.Web/wwwroot/js/site.js`: geçti.
- Tarayıcı: 1440px ana sayfa, boş/dolu sepet, ürün detayı, admin özeti ve ürün listesi incelendi.
- 390px katalog, sepet ve admin kontrol edildi. Sepet ve giriş ekranında 320px genişlikte sayfa taşması yok.
- Sepete ürün ekleme, adet artırma, doğru toplam (2 defter + 2 vazo = ₺1.404,00), satır sırasını ve odağı koruma doğrulandı.
- Katalogda Vazo araması, mobil menü açılması ve yanlış şifrede okunabilir hata mesajı kontrol edildi.
- Görsel kanıtlar: `output/playwright/revision-*.png`.
- Son bağımsız incelemenin kontrast bulgusu giderildi: `--muted: #62655e`, panel üzerinde 5.05:1 ve ana zeminde 5.48:1 kontrast sağlar.
- Son 39 kontrolün izole kayıtları: `App_Data/checks/2ccd58c4a43d464f9710f6647c74471e`.

Yeni paket, ödeme servisi veya veri tabanı değişikliği eklenmedi. Sipariş, stok ve yetkilendirme kontrolleri mevcut API sözleşmeleriyle çalışır.
