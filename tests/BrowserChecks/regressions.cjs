const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { randomUUID } = require('node:crypto');
const [url, outputArgument] = process.argv.slice(2);
if (!url) throw new Error('Ayrı test veritabanıyla çalışan host adresini argüman olarak verin.');
const output = outputArgument || path.join(__dirname, 'output');
fs.mkdirSync(output, { recursive: true });
const checks = [];
const errors = [];
let currentPage;
const record = name => { checks.push(name); console.log('PASS ' + name); };

async function client(context, email, password) {
  let token = (await (await context.request.get(url + '/api/auth/csrf')).json()).token;
  assert.equal((await context.request.post(url + '/api/auth/login', {
    headers: { 'X-CSRF-TOKEN': token }, data: { email, password }
  })).status(), 200);
  token = (await (await context.request.get(url + '/api/auth/csrf')).json()).token;
  return async (route, method = 'GET', data, status = 200) => {
    const response = await context.request.fetch(url + route, { method, data, headers: { 'X-CSRF-TOKEN': token } });
    assert.equal(response.status(), status, route + ': ' + (await response.text()).slice(0, 300));
    return status === 204 ? null : response.json();
  };
}
const orderData = (id, quantity) => ({ deliveryAddress: 'Deneme Mahallesi Kontrol Sokak No: 1 İstanbul',
  requestId: randomUUID(), orderLines: [{ productId: id, quantity }] });

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const manager = await browser.newContext({ viewport: { width: 1536, height: 1000 } });
    const customer = await browser.newContext({ viewport: { width: 1536, height: 1000 } });
    const adminApi = await client(manager, 'admin@homelivestore.local', 'Admin123!');
    const customerApi = await client(customer, 'musteri@homelivestore.local', 'Musteri123!');
    const makePage = async context => {
      const page = await context.newPage();
      currentPage = page;
      page.on('pageerror', error => errors.push(error.message));
      return page;
    };
    const suffix = Date.now().toString(36);
    const firstCategory = await adminApi('/api/admin/categories', 'POST', { definition: 'A Kontrol ' + suffix, description: '', isActive: true }, 201);
    const lastCategory = await adminApi('/api/admin/categories', 'POST', { definition: 'Z Kontrol ' + suffix, description: '', isActive: true }, 201);
    const productData = { categoryId: lastCategory.id, definition: 'Onarım Bambu Ürünü ' + suffix, description: '',
      barcode: 'FIX-' + suffix, price: 100, vatRate: 0, stockAmount: 10, coverPhoto: '/images/products/board.png', isActive: true };
    const product = await adminApi('/api/admin/products', 'POST', productData, 201);

    const adminPage = await makePage(manager);
    await adminPage.goto(url + '/Admin/Products', { waitUntil: 'networkidle' });
    await adminPage.locator('[data-edit-product="' + product.id + '"]').click();
    const categoryInput = adminPage.locator('[data-product-form] [name=categoryId]');
    assert.equal(Number(await categoryInput.inputValue()), lastCategory.id);
    await adminPage.locator('[data-admin-product-search]').fill(productData.barcode);
    await adminPage.waitForFunction(id => {
      const products = JSON.parse(document.querySelector('[data-admin-products-list]').dataset.products || '[]');
      return products.length === 1 && products[0].id === id;
    }, product.id);
    assert.equal(Number(await categoryInput.inputValue()), lastCategory.id);
    const saved = adminPage.waitForResponse(response => response.url().endsWith('/api/admin/products/' + product.id) && response.request().method() === 'PUT');
    await adminPage.locator('[data-product-form] button[type=submit]').click();
    assert.equal((await saved).status(), 200);
    assert.equal((await adminApi('/api/products/' + product.id)).categoryId, lastCategory.id);
    record('Açık ürün formundaki kategori, arama ve kayıt sırasında korunuyor');
    await adminPage.close();

    // İki yanıtın sırasını belirleyip eski başarılı yanıtı ve eski hatayı ayrı ayrı deniyoruz.
    for (const oldFails of [false, true]) {
      const page = await makePage(manager);
      await page.goto(url + '/Admin/Products', { waitUntil: 'networkidle' });
      let releaseOld, markOldReady;
      const gate = new Promise(resolve => { releaseOld = resolve; });
      const ready = new Promise(resolve => { markOldReady = resolve; });
      await page.route('**/api/admin/products?**', async route => {
        if (new URL(route.request().url()).searchParams.get('search') !== 'Bambu') return route.continue();
        const response = await route.fetch();
        markOldReady();
        await gate;
        if (oldFails) await route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Geciken eski arama hatası' }) });
        else await route.fulfill({ response });
      });
      try {
        const olderResponse = page.waitForResponse(response => response.url().includes('/api/admin/products?') && new URL(response.url()).searchParams.get('search') === 'Bambu');
        await page.locator('[data-admin-product-search]').fill('Bambu');
        await ready;
        const newestResponse = page.waitForResponse(response => response.url().includes('/api/admin/products?') && new URL(response.url()).searchParams.get('search') === 'Pamuk');
        await page.locator('[data-admin-product-search]').fill('Pamuk');
        await newestResponse;
        await page.waitForFunction(() => {
          const products = JSON.parse(document.querySelector('[data-admin-products-list]').dataset.products || '[]');
          return products.length === 1 && products[0].definition.includes('Pamuk');
        });
        releaseOld();
        await (await olderResponse).finished();
        await page.waitForTimeout(100);
        const products = await page.locator('[data-admin-products-list]').innerText();
        assert(products.includes('Pamuk') && !products.includes('Bambu'));
        assert(!(await page.locator('[data-admin-message]').innerText()).includes('Geciken eski arama hatası'));
        record(oldFails ? 'Yönetimde geciken eski hata güncel sonuçları bozmuyor' : 'Yönetimde geciken eski yanıt güncel sonuçları ezmiyor');
      } finally { releaseOld(); await page.close(); }
    }

    async function openCart(id) {
      const page = await makePage(customer);
      await page.goto(url + '/Products/Detail/' + id, { waitUntil: 'networkidle' });
      await page.evaluate(() => localStorage.removeItem('kurumsal-cart'));
      await page.locator('[data-add-cart]').click();
      await page.goto(url + '/Cart', { waitUntil: 'networkidle' });
      await page.locator('#delivery-address').fill('Deneme Mahallesi Kontrol Sokak No: 1 İstanbul');
      return page;
    }

    const checkoutPage = await openCart(product.id);
    const ordersBefore = await customerApi('/api/orders');
    const requests = [];
    let releaseCheckout, markCheckoutReady;
    const checkoutGate = new Promise(resolve => { releaseCheckout = resolve; });
    const checkoutReady = new Promise(resolve => { markCheckoutReady = resolve; });
    await checkoutPage.route('**/api/orders', async route => {
      if (route.request().method() !== 'POST') return route.continue();
      requests.push(route.request().postDataJSON());
      const response = await route.fetch();
      markCheckoutReady();
      await checkoutGate;
      await route.fulfill({ response }).catch(() => {});
    });
    try {
      await checkoutPage.locator('[data-checkout-button]').click();
      await checkoutReady;
      for (const selector of ['[data-checkout-button]', '[data-cart-qty]', '[data-cart-adjust][data-step="1"]', '[data-remove-cart]', '#delivery-address']) {
        assert(await checkoutPage.locator(selector).isDisabled(), 'Pending checkout locks ' + selector);
      }
      // Düğme dışında tekrar submit tetiklense de ikinci HTTP isteği çıkmamalı.
      await checkoutPage.locator('[data-checkout-form]').evaluate(form => form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })));
      await checkoutPage.waitForTimeout(100);
      assert.equal(requests.length, 1);
      await checkoutPage.screenshot({ path: path.join(output, 'checkout-locked.png'), fullPage: true });
      releaseCheckout();
      await checkoutPage.waitForURL('**/Orders/Detail/*');
      const ordersAfter = await customerApi('/api/orders');
      const created = ordersAfter.filter(order => !ordersBefore.some(previous => previous.id === order.id));
      assert.equal(created.length, 1);
      assert.equal(created[0].orderLines[0].quantity, 1);
      record('Yavaş sipariş sırasında adet/adres kilitli kalıyor ve yalnızca bir sipariş oluşuyor');
    } finally { releaseCheckout(); await checkoutPage.close(); }

    const retryPage = await openCart(product.id);
    const retryRequests = [];
    await retryPage.route('**/api/orders', async route => {
      if (route.request().method() !== 'POST') return route.continue();
      retryRequests.push(route.request().postDataJSON());
      if (retryRequests.length === 1) return route.fulfill({ status: 503, contentType: 'application/problem+json', body: JSON.stringify({ detail: 'Geçici bağlantı hatası' }) });
      return route.continue();
    });
    await retryPage.locator('[data-checkout-button]').click();
    await retryPage.locator('[data-checkout-message].error').waitFor();
    for (const selector of ['[data-checkout-button]', '[data-cart-qty]', '[data-remove-cart]', '#delivery-address']) {
      assert(await retryPage.locator(selector).isEnabled(), 'Failed checkout unlocks ' + selector);
    }
    await retryPage.locator('[data-checkout-button]').click();
    await retryPage.waitForURL('**/Orders/Detail/*');
    assert.equal(retryRequests.length, 2);
    assert.equal(retryRequests[0].requestId, retryRequests[1].requestId);
    record('Başarısız sipariş kontrolleri açıyor ve yeniden denemede aynı istek kimliği korunuyor');
    await retryPage.close();

    const highStock = await adminApi('/api/admin/products', 'POST', { ...productData, barcode: 'LIMIT-' + suffix, definition: 'Adet Kontrol ' + suffix, stockAmount: 1500 }, 201);
    const limitPage = await openCart(highStock.id);
    assert.equal(await limitPage.locator('meta[name="maximum-order-quantity"]').getAttribute('content'), '999');
    assert.equal(await limitPage.locator('[data-cart-qty]').getAttribute('max'), '999');
    await limitPage.locator('[data-cart-qty]').fill('1000');
    await limitPage.locator('#delivery-address').click();
    await limitPage.waitForFunction(() => document.querySelector('[data-cart-qty]').value === '1');
    assert((await limitPage.locator('[data-toast]').innerText()).includes('999'));
    await limitPage.locator('[data-cart-qty]').fill('999');
    await limitPage.locator('#delivery-address').click();
    await limitPage.waitForFunction(() => document.querySelector('[data-cart-quantity]').textContent === '999');
    assert(await limitPage.locator('[data-cart-adjust][data-step="1"]').isDisabled());
    const limitResponse = limitPage.waitForResponse(response => response.url().endsWith('/api/orders') && response.request().method() === 'POST');
    await limitPage.locator('[data-checkout-button]').click();
    const accepted = await limitResponse;
    assert.equal(accepted.status(), 201);
    await limitPage.waitForURL('**/Orders/Detail/*');
    const acceptedOrderId = Number(limitPage.url().split('/').pop());
    assert.equal((await customerApi('/api/orders/' + acceptedOrderId)).orderLines[0].quantity, 999);
    record('Sepet 1000 adedi reddediyor, üst sınır 999 arayüz ve API ile aynı');
    await limitPage.close();

    for (const searches of [['Masa', 'masa', 'MASA'], ['Bambu', 'bambu', 'BAMBU']]) {
      const counts = [];
      for (const search of searches) counts.push((await customerApi('/api/products?search=' + encodeURIComponent(search))).totalCount);
      assert(counts[0] > 0 && counts.every(count => count === counts[0]), JSON.stringify({ searches, counts }));
    }
    const searchPage = await makePage(customer);
    await searchPage.goto(url + '/Products', { waitUntil: 'networkidle' });
    await searchPage.locator('[data-product-search]').fill('MASA');
    await searchPage.waitForFunction(() => document.querySelector('[data-product-list]').textContent.includes('Masa Lambası'));
    record('Büyük/küçük harf araması aynı ürünleri buluyor; MASA mağazada da sonuç veriyor');
    await searchPage.close();

    const stockBeforeInvalid = (await customerApi('/api/products/' + product.id)).stockAmount;
    const invalid = orderData(product.id, 1);
    invalid.orderLines.push(null);
    const validation = await customerApi('/api/orders', 'POST', invalid, 400);
    assert(validation.errors);
    assert.equal((await customerApi('/api/products/' + product.id)).stockAmount, stockBeforeInvalid);
    record('Boş sipariş satırı 500 yerine 400 doğrulama hatası veriyor; stok korunuyor');

    const returnData = { ...productData, barcode: 'RETURN-' + suffix, definition: 'İade Kontrol ' + suffix, stockAmount: 1 };
    const returnProduct = await adminApi('/api/admin/products', 'POST', returnData, 201);
    const returnOrder = await customerApi('/api/orders', 'POST', orderData(returnProduct.id, 1), 201);
    await adminApi('/api/admin/products/' + returnProduct.id, 'PUT', { ...returnData, stockAmount: 2147483647, originalStockAmount: 0 });
    const ordersPage = await makePage(manager);
    await ordersPage.goto(url + '/Admin/Orders', { waitUntil: 'networkidle' });
    const rejectedCancellation = ordersPage.waitForResponse(response => response.url().endsWith('/api/admin/orders/' + returnOrder.id + '/status') && response.request().method() === 'PUT');
    await ordersPage.locator('[data-order-status="' + returnOrder.id + '"]').selectOption('Canceled');
    assert.equal((await rejectedCancellation).status(), 409);
    await ordersPage.waitForFunction(id => {
      const select = document.querySelector('[data-order-status="' + id + '"]');
      return select?.value === 'Preparing' && !select.disabled;
    }, returnOrder.id);
    assert.equal((await customerApi('/api/products/' + returnProduct.id)).stockAmount, 2147483647);
    await customerApi('/api/products');
    await ordersPage.screenshot({ path: path.join(output, 'stock-return-protected.png'), fullPage: true });
    await adminApi('/api/admin/products/' + returnProduct.id, 'PUT', { ...returnData, stockAmount: 2147483646, originalStockAmount: 2147483647 });
    const acceptedCancellation = ordersPage.waitForResponse(response => response.url().endsWith('/api/admin/orders/' + returnOrder.id + '/status') && response.request().method() === 'PUT');
    await ordersPage.locator('[data-order-status="' + returnOrder.id + '"]').selectOption('Canceled');
    assert.equal((await acceptedCancellation).status(), 200);
    await ordersPage.waitForFunction(id => document.querySelector('[data-order-status="' + id + '"]')?.value === 'Canceled', returnOrder.id);
    assert.equal((await customerApi('/api/products/' + returnProduct.id)).stockAmount, 2147483647);
    record('Taşan stok iadesi reddediliyor; durum korunuyor, stok düzeltildiğinde iptal tamamlanıyor');
    await ordersPage.close();
    assert.deepEqual(errors, []);
    record('Düzeltme senaryolarında JavaScript istisnası oluşmuyor');
    await manager.close();
    await customer.close();
    fs.writeFileSync(path.join(output, 'fix-regressions.json'), JSON.stringify({ passed: true, checks, errors }, null, 2));
  } catch (error) {
    if (currentPage) await currentPage.screenshot({ path: path.join(output, 'fix-regression-failure.png'), fullPage: true }).catch(() => {});
    fs.writeFileSync(path.join(output, 'fix-regressions.json'), JSON.stringify({ passed: false, checks, errors, error: String(error) }, null, 2));
    throw error;
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exit(1); });
