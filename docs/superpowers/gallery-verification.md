# Home & Living Store galeri doğrulaması

19 Ekim 2026 tarihinde ayrı SQLite veritabanı ve geçici yerel HTTP hostu üzerinde kontrol edildi.

- Mağaza adı: **Home & Living Store**.
- Çözüm ve namespace kökü: **HomeLiveStore**.
- Web çalıştırma profili: **HomeLiveStore**. Debug çıktısı: **HomeLiveStore.exe**.
- Derleme: 0 hata, 0 uyarı.
- Domain: 4 kontrol geçti.
- HTTP entegrasyon: 39 kontrol geçti. Galeri sırası, ekleme/kaldırma, alan gönderilmediğinde mevcut görsellerin korunması, geçersiz adreslerin reddi ve yönetici yetkisi dahil.
- Genel tarayıcı akışları: 25 kontrol geçti; 15 sayfa 1536, 768, 390 ve 320 piksel genişlikte yatay taşma olmadan açıldı.
- Önceki hatalar: 8 regresyon kontrolü geçti; satış sırasında stok koruması, sipariş durumu hatasından sonra yeniden işlem, güncel arama yanıtı ve kayıt bildirimleri korundu.
- Galeri: 11 tarayıcı kontrolü geçti. Oklar, son/ilk görsel geçişi, küçük görseller, klavye, iki yönde dokunmatik kaydırma, 10 ek görsel sınırı, kendi içinde kayan küçük görsel şeridi ve eksik görsel davranışı doğrulandı.
- Galeri ve yeni marka 320, 390, 575, 768, 1024, 1180, 1200 ve 1536 pikselde kontrol edildi.

Galeri API'si mevcut `ProductImages` tablosunu kullanır; yeni tablo veya kolon eklenmedi. Ana mağaza veritabanı testler için kullanılmadı. Otomatik tarayıcı kontrolleri Edge üzerinde yapıldı; fiziksel telefon testi yapılmadı.

Projeyle gelen kontrolleri tekrar çalıştırmak için:

```powershell
pwsh -File scripts/verify.ps1
```

Bu komut derleme, domain ve HTTP kontrollerini çalıştırır. Tarayıcı kontrolleri geliştirme sırasında ayrıca uygulandı; uygulamayı kullanmak için Node.js gerekmez.
