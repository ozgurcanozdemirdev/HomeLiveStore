# HomeLiveStore hata düzeltme doğrulaması

19 Ekim 2026 tarihli ayrıntılı incelemede bulunan yedi hata düzeltildi. Mevcut veritabanı şeması değiştirilmedi.

| İnceleme bulgusu | Son davranış | Doğrulama |
|---|---|---|
| Bekleyen siparişte ikinci gönderim | Adet/adres kontrolleri kilitli; submit işleyicisi ikinci gönderimi reddeder. Hata sonrası yeniden deneme mümkündür. | Geciken yanıtla yalnızca bir sipariş; 503 sonrası başarılı tekrar ve aynı requestId. |
| Ürün formunun kategorisinin değişmesi | Liste yenilense de açık formdaki kategori korunur. | Arama sonrası seçili kategori ve kayıtlı kategori aynı. |
| Yönetimde eski arama yanıtı | Her yükleme kendi sıra numarasını taşır; eski başarılı yanıt veya hata DOM'u değiştirmez. | İki ayrı gecikmeli yanıt senaryosu. |
| Stok iadesinde taşma | Artış üst sınır koşuluyla yapılır; taşacak iptal 409 döner ve transaction geri alınır. | İki ürünlü siparişte önceki stok iadesi de geri alınır. Sınır düzeltildikten sonra iptal bir kez tamamlanır. |
| Harf duyarlı arama | SQLite filtresi Türkçe ve ASCII harf duyarsız karşılaştırma kullanır. | İNCİ/inci, IŞIK/ışık, ÇALIŞMA/çalışma, KÖŞESİ/köşesi ve barkod sorguları. |
| Null sipariş satırı | DTO ve servis doğrulaması hatalı satırı reddeder. | 400 doğrulama yanıtı; sipariş ve stok değişmez. |
| Sepet/API adet sınırı | Her iki taraf aynı 999 sabitini kullanır; birleşen satırların toplamı da denetlenir. | 1000 reddedilir; 999 kabul edilir; 600+600 aynı ürün isteği reddedilir. |

## Sonuçlar

- Derleme: 0 hata, 0 uyarı.
- Domain: 6 kontrol geçti.
- HTTP: 43 entegrasyon kontrolü geçti.
- Genel tarayıcı: 25 kontrol geçti; 15 sayfa 1536, 768, 390 ve 320 pikselde kontrol edildi.
- Önceki hata regresyonları: 8 kontrol geçti.
- Galeri: 11 kontrol geçti; dokunmatik kaydırma dahil.
- Yeni hata regresyonları: 10 kontrol geçti; JavaScript istisnası oluşmadı.

Kontroller ayrı çalışma kopyasında ve geçici SQLite veritabanlarında yürütüldü. Tarayıcı kontrolleri Windows üzerinde Edge ile yapıldı; fiziksel telefon testi yapılmadı.

## Tekrar çalıştırma

Proje kökünde:

```powershell
pwsh -File scripts/verify.ps1
```

Bu komut derleme, 6 domain ve 43 HTTP kontrolünü çalıştırır. Yeni hata senaryolarının tarayıcı kontrollerini de eklemek için:

```powershell
npm ci --prefix tests/BrowserChecks
pwsh -File scripts/verify.ps1 -BrowserChecks
```

Tarayıcı kontrolleri için Node.js 20 veya üzeri ve Microsoft Edge gerekir. Playwright yalnızca test bağımlılığıdır. Üretim uygulaması yeni bir NuGet paketine veya Node.js'e ihtiyaç duymaz.
