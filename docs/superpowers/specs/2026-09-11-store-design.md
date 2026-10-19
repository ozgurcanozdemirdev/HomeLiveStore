# Home & Living Store tasarımı

## Amaç ve kapsam
.NET 10 ders projesinde Türkçe, çalışan mağaza ve admin paneli. Ödeme, havale, sanal POS yok: giriş yapan müşteri sepetini ve teslimat adresini onaylayınca sipariş oluşur. Mevcut beş katman korunur, yeni NuGet bağımlılığı eklenmez. SQLite mevcut sağlayıcıdır.

## Mimari
Domain varlık ve sipariş kuralları; Application DTO ve servis sözleşmeleri; Infrastructure EF Core, veri ve servis uygulamaları; Api JSON controller'ları ve bağımsız host; Web MVC ekranları ve aynı API controller'larını application part olarak barındıran tek origin host. Web tek komutla çalışır, ayrıca API bağımsız çalışabilir. İki host aynı konfigüre edilebilir SQLite dosyasını kullanır. Geliştirmede örnek ürün ve demo hesaplar otomatik oluşturulur; üretimde demo hesaplar eklenmez.

## HTTP sözleşmesi
JSON camelCase, enum string. Hata: ProblemDetails {title,detail,status,errors?}. Cookie authentication, Manager ve Customer rolleri. Güvenli olmayan tüm API çağrıları X-CSRF-TOKEN başlığı ister; GET /api/auth/csrf -> {token}. Giriş/kayıt/çıkış sonrası CSRF yenilenir.

- POST /api/auth/register {name,surname,email,password} -> kullanıcı; POST /api/auth/login {email,password} -> kullanıcı; POST /api/auth/logout -> 204; GET /api/auth/me -> kullanıcı veya 401.
- Kullanıcı {id,name,surname,email,role}; kayıt rolü her zaman Customer.
- GET /api/categories -> Category[]; GET /api/products?search=&categoryId=&sort=newest|priceAsc|priceDesc|name&page=1&pageSize=12 -> {items,totalCount,page,pageSize,totalPages}; GET /api/products/{id} -> Product.
- Product {id,categoryId,categoryName,definition,description,barcode,price,vatRate,unitPrice,stockAmount,coverPhoto,isActive}. price KDV hariç, unitPrice KDV dahil ve kuruşa yuvarlanır. Görseller /images/... veya https URL. Category {id,definition,description,isActive}.
- GET /api/admin/products (aynı sayfalama); POST /api/admin/products; PUT /api/admin/products/{id}; DELETE /api/admin/products/{id}. Kaydetme {categoryId,definition,description,barcode,price,vatRate,stockAmount,coverPhoto,isActive}.
- GET /api/admin/categories -> Category[]; POST /api/admin/categories; PUT /api/admin/categories/{id}; DELETE /api/admin/categories/{id}; kaydetme {definition,description,isActive}. Ürünü olan kategori silinmez (409).
- POST /api/orders {deliveryAddress,requestId,orderLines:[{productId,quantity}]} -> Order. requestId UUID ile çift gönderim önlenir. Fiyat ve müşteri sunucudan alınır. Aynı ürün satırları birleştirilir; adet ve stok doğrulanır. Sipariş kaydı ve stok düşümü atomiktir.
- GET /api/orders -> kullanıcının Order[]; GET /api/orders/{id} -> sadece sahibi/Manager. POST /api/orders/{id}/cancel -> Order; müşteri sadece Preparing iptal eder.
- GET /api/admin/orders -> Order[]; PUT /api/admin/orders/{id}/status {status} -> Order. Durumlar Preparing, Shipped, Completed, Canceled. Preparing -> Shipped/Canceled, Shipped -> Completed. Aynı duruma geçiş yan etkisiz; iptalde stok yalnız bir kez geri gelir.
- Order {id,orderDate,totalAmount,deliveryAddress,status,customerName,customerEmail,orderLines:[{productId,productName,unitPrice,quantity,lineTotalAmount}]}. Ürün adı/fiyat/vergi sipariş anında saklanır.
- GET /api/admin/users -> kullanıcı listesi (parola yok).
- GET /api/admin/dashboard -> {productCount,categoryCount,customerCount,orderCount,pendingOrderCount,totalRevenue,lowStockProducts:Product[],recentOrders:Order[]}.
- GET /health -> 200; geliştirmede /openapi/v1.json.

## Ekranlar
GET / anasayfa; /Products katalog; /Products/Detail/{id}; /Cart; /Account/Login; /Account/Register; /Orders; /Orders/Detail/{id}. /Admin dashboard; /Admin/Products; /Admin/Categories; /Admin/Orders; /Admin/Users. Admin URL'leri sunucuda Manager yetkisi ister. Ürün ve kategori CRUD formları, sipariş durum işlemleri gerçek API'ye bağlıdır. Sepet localStorage'da sadece ürün kimliği/adet tutar; geçerli fiyat ve stok API'den okunur.

## Tasarım
11 Eylül görsel revizyonu: kırık beyaz yüzeyler, mürekkep rengi metin ve kiremit vurgu. Yerel Manrope tipografisi, fotoğraf ağırlıklı ana sayfa ve kutusuz ürün kataloğu. Sepet satırlarında adet kontrolleri ve satır toplamı; admin panelinde düz yan menü, aktif sayfa işareti ve yoğun tablolar. Mobil menü, dar ekranda iki sütunlu katalog, etiketli formlar, görünür odak ve yükleniyor/boş/hata durumları. Altı üretilmiş fotoğraf ve lisanslı font dosyaları yereldir; sayfalar dış görsel/font servisine ihtiyaç duymaz.

## Kabul kriterleri
Çözüm uyarısız derlenir. Yeni üyelik/giriş/çıkış, müşteri yetkisi, fiyat manipülasyonu reddi, stok yetersizliğinde atomiklik, idempotency, sipariş sahipliği, iptal stok iadesi ve admin CRUD gerçek HTTP ve SQLite ile doğrulanır. Masaüstü/mobil mağaza ve admin tarayıcıda kontrol edilir. README başlatma, demo hesaplar, API ve test komutlarını içerir.

## Seçim gerekçesi
MVC'yi korumak dersin mevcut yapısına uyar ve ikinci frontend araç zinciri gerektirmez. Ayrı SPA eklemek veya mikroservislere bölmek bu kapsamda gereksiz kurulum ve entegrasyon maliyeti oluşturur. Kullanıcının özerk ilerleme talimatı kapsamında yerel düzenleme ve testler onay beklemeden yürütülür. Klasör Git deposu değildir; commit/worktree adımları uygulanamaz.
