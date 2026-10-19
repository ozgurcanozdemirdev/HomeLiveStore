# Home & Living Store doğrulama raporu

Tarih: 19 Ekim 2026.

## Tasarım kapsamı

Ana sayfa, katalog, ürün detayı, giriş, kayıt, sepet, sipariş listesi ve detayı; yönetim özeti, ürünler, kategoriler, siparişler, kullanıcılar; gizlilik ve hata ekranı ortak Bootstrap 5 ve özel CSS tasarımına geçirildi. Marka simgesizdir. Krem, kiremit ve koyu gri renkler kullanılır.

Ana sayfadaki marka, ana başlık, ana buton, ürün bölümü başlığı, ilk ürünün görseli/metinleri/butonu, alt tanıtım ve footer; giriş ekranındaki marka, başlık, form etiketi, gönderme butonu ve footer tarayıcıda ölçüldü. Sol kenarlar arasındaki fark 0 pikseldir.

## Derleme ve testler

- .NET SDK 10.0.402 ile çözüm derlemesi: 0 hata, 0 uyarı.
- 4 domain/doğrulama kontrolü başarılı. Fiyat sınırları Türkçe, İngilizce ve Almanca kültürlerde ayrıca kontrol edildi.
- Mevcut 36 gerçek HTTP entegrasyon kontrolü başarılı.
- 25 tarayıcı kontrolü başarılı. Giriş/kayıt/çıkış, kategori bağlantıları, katalog araması, ürün detayı, sepet miktarı, sipariş oluşturma/iptal, yönetici oturumu, ürün/kategori ekleme-düzenleme-silme ve yönetim sayfaları çalıştırıldı.
- 15 sayfa 1536, 768, 390 ve 320 piksel genişliklerde açıldı. Belge düzeyinde yatay taşma bulunmadı; geniş yönetim tabloları kendi alanlarında kaydırılabilir.
- Mobil menünün açılıp kapanması ve tarayıcıda JavaScript istisnası oluşmaması doğrulandı.

Ekran görüntüleri: `docs/screenshots/`. Bunlar çalışan uygulamadan, yalnızca örnek kayıtlar içeren ayrı bir SQLite veritabanıyla alındı.

## Korunan davranış ve küçük uyumluluk düzeltmesi

Domain, Infrastructure ve API iş kuralları korunmuştur. Proje, klasör ve namespace adları `HomeLiveStore` olarak düzenlenmiştir. Var olan gizlilik görünümüne ulaşmak için MVC `Privacy` eylemi eklenmiştir.

Gerçek ürün kaydı sırasında, mevcut `Range(typeof(decimal), "0.01", "999999999")` doğrulamasının Türkçe sistem kültüründe sunucu hatası ürettiği görüldü. Sınırlar değiştirilmeden `ParseLimitsInInvariantCulture = true` eklendi. Bu davranış için üç kültürde alt/üst sınır ve geçersiz tutarları denetleyen regresyon kontrolü bulunmaktadır.

## Tekrar çalıştırma

```powershell
dotnet build HomeLiveStore.slnx
pwsh -File scripts/verify.ps1
```

`verify.ps1` kendi geçici SQLite dosyasını açar. Tarayıcı kontrolleri de teslim edilen mağazanın veritabanından ayrı bir dosyada yürütülmüştür. Gerçek ödeme, internet yayını, fiziksel telefon testi ve üretim ortamı bu doğrulamanın kapsamında değildir.
