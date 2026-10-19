# Mağaza görsel revizyonu

Kullanıcı mevcut tasarımın genel bir AI şablonu gibi göründüğünü belirtti. Çalışan ders projesi akışlarını koruyarak mağaza ve admin arayüzü yeniden düzenlenecek.

## Görsel yön

Ev eşyası kataloğu: kırık beyaz, mürekkep siyahı, tek kiremit vurgu. İnce çizgiler, düz yüzeyler, gerçekçi ürün fotoğrafları, sade sans tipografi. Yuvarlak kartlar, arka plan gradyanları, gölgeler ve yinelenen dev serif başlıklar kaldırılacak. Ana sayfa fotoğraf ve büyük ama kısa başlıkla açılacak; katalog ürünleri öne çıkaracak. Sepet çizgilerle ayrılan ürün satırları ve belirgin sipariş özeti kullanacak. Admin daha yoğun, tablo odaklı ve aktif menüsü belli bir çalışma alanı olacak.

## Uygulama ve kontrol

1. Ortak yerleşim, navigasyon ve tasarım değişkenlerini yeniden kur.
2. Ana sayfa, katalog, ürün detayı, sepet ve üyelik sayfalarını bu dile taşı.
3. Ürün fotoğraflarını yerel statik dosyalarla bağla; kullanıcı görsel bağlantılarını koru.
4. Admin menüsü, tablolar, formlar ve özetleri düzenle.
5. Mevcut API ve DOM veri kancalarını koru. 39 mevcut alan/HTTP kontrolünü çalıştır; tarayıcıda masaüstü ve mobil sepet, katalog ve admini denetle.

Yeni paket veya ödeme entegrasyonu eklenmeyecek. Görsel revizyon, sipariş/stok/yetki sözleşmesini değiştirmeyecek.
