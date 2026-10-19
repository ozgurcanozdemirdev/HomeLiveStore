# Tarayıcı regresyon kontrolleri

Bu testler Windows'ta Microsoft Edge kullanır. Uygulamayı çalıştırmak için Node.js gerekmez; yalnızca bu ek testleri çalıştırmak için Node.js 20 veya üzeri ve test klasörünün Playwright bağımlılığı gerekir.

Proje kökünden bağımlılığı kurun:

```powershell
npm ci --prefix tests/BrowserChecks
```

Ardından ayrı veritabanı ve geçici port kullanan doğrulamayı başlatın:

```powershell
pwsh -File scripts/verify.ps1 -BrowserChecks
```

Kendi ayrı test hostunuz varsa `npm --prefix tests/BrowserChecks test -- http://127.0.0.1:PORT C:/TestSonuclari` kullanılabilir. Testler örnek ürün ve sipariş oluşturduğu için günlük kullanılan mağaza veritabanını test hostuna bağlamayın.
