(() => {
  const cartKey = "kurumsal-cart";
  const checkoutRequestKey = "kurumsal-checkout-request";
  const money = new Intl.NumberFormat("tr-TR", { style: "currency", currency: "TRY" });
  const dateTime = new Intl.DateTimeFormat("tr-TR", { dateStyle: "medium", timeStyle: "short" });
  let csrfToken = "";
  let currentUser = null;

  const qs = (selector, root = document) => root.querySelector(selector);
  const qsa = (selector, root = document) => Array.from(root.querySelectorAll(selector));
  // Sayfa bu sınırı API'nin kullandığı BasketLineRequest sabitinden alır.
  const maximumOrderQuantity = Number(qs("meta[name='maximum-order-quantity']")?.content) || 999;
  const page = () => qs("[data-page]")?.dataset.page ?? "";
  const esc = (value) => String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
  const toMoney = (value) => money.format(Number(value || 0));
  const toDate = (value) => value ? dateTime.format(new Date(value)) : "-";
  const cataloguePhotos = Object.fromEntries(["blanket", "vase", "lamp", "notebook", "board"]
    .map((name) => [`/images/products/${name}.svg`, `/images/products/${name}.png`]));
  const imageOf = (item) => cataloguePhotos[item?.coverPhoto] || item?.coverPhoto || "/images/product-placeholder.svg";
  const safeLocalUrl = (value) => {
    try {
      const url = new URL(value || "/", window.location.origin);
      return url.origin === window.location.origin && url.pathname.startsWith("/") ? `${url.pathname}${url.search}${url.hash}` : "/";
    } catch {
      return "/";
    }
  };

  async function api(path, options = {}) {
    const headers = new Headers(options.headers || {});
    headers.set("Accept", "application/json");
    const method = (options.method || "GET").toUpperCase();
    if (method !== "GET") {
      headers.set("Content-Type", "application/json");
      if (csrfToken) headers.set("X-CSRF-TOKEN", csrfToken);
    }

    const response = await fetch(path, {
      credentials: "same-origin",
      ...options,
      method,
      headers
    });

    if (response.status === 204) return null;
    const text = await response.text();
    const contentType = response.headers.get("content-type") || "";
    let data = null;
    if (text && (contentType.includes("application/json") || contentType.includes("application/problem+json"))) {
      try {
        data = JSON.parse(text);
      } catch {
        data = null;
      }
    }
    if (!response.ok) {
      const validation = data?.errors
        ? Object.values(data.errors).flat().filter(Boolean).join(" ")
        : "";
      const detail = validation || data?.detail || data?.title || text || response.statusText || "İşlem tamamlanamadı.";
      const error = new Error(detail);
      error.status = response.status;
      error.data = data;
      throw error;
    }
    return data;
  }

  async function refreshCsrf() {
    try {
      const data = await api("/api/auth/csrf");
      csrfToken = data?.token || "";
      qs('meta[name="csrf-token"]')?.setAttribute("content", csrfToken);
    } catch {
      csrfToken = "";
    }
  }

  async function refreshUser() {
    try {
      currentUser = await api("/api/auth/me");
    } catch {
      currentUser = null;
    }
    renderAccount();
  }

  function getCart() {
    try {
      const parsed = JSON.parse(localStorage.getItem(cartKey) || "[]");
      return Array.isArray(parsed)
        ? parsed.filter((line) => Number(line.productId) > 0 && Number(line.quantity) > 0)
        : [];
    } catch {
      return [];
    }
  }

  function setCart(lines) {
    localStorage.setItem(cartKey, JSON.stringify(lines));
    renderCartCount();
  }

  function addToCart(productId, quantity = 1) {
    const lines = getCart();
    const existing = lines.find((line) => line.productId === productId);
    if (Number(existing?.quantity || 0) + quantity > maximumOrderQuantity) {
      showToast(`Bir üründen en fazla ${maximumOrderQuantity} adet sipariş verebilirsiniz.`);
      return false;
    }
    if (existing) existing.quantity = Number(existing.quantity) + quantity;
    else lines.push({ productId, quantity });
    setCart(lines);
    return true;
  }

  function updateCartLine(productId, quantity) {
    const lines = getCart();
    const index = lines.findIndex((line) => line.productId === productId);
    if (quantity <= 0) {
      if (index >= 0) lines.splice(index, 1);
    } else if (index >= 0) {
      lines[index].quantity = quantity;
    } else {
      lines.push({ productId, quantity });
    }
    setCart(lines);
  }

  function renderCartCount() {
    const count = getCart().reduce((sum, line) => sum + Number(line.quantity || 0), 0);
    qsa("[data-cart-count]").forEach((node) => { node.textContent = count; });
  }

  function renderAccount() {
    const area = qs("[data-account-area]");
    if (area) {
      area.innerHTML = currentUser
        ? `<span class="ghost-link">${esc(currentUser.name)} ${esc(currentUser.surname)}</span><button class="button-link" type="button" data-logout>Çıkış</button>`
        : '<a class="ghost-link" href="/Account/Login">Giriş Yap</a><a class="button-link" href="/Account/Register">Üye Ol</a>';
    }
    qsa("[data-manager-link]").forEach((link) => {
      link.hidden = currentUser?.role !== "Manager";
    });
    qs("[data-logout]")?.addEventListener("click", async () => {
      try {
        await api("/api/auth/logout", { method: "POST" });
        await refreshCsrf();
        window.location.href = "/";
      } catch (error) {
        showToast(error.message);
      }
    });
  }

  function productCard(product) {
    return `
      <article class="card product-card">
        <a class="product-media" href="/Products/Detail/${product.id}">
          <img src="${esc(imageOf(product))}" alt="${esc(product.definition)}" loading="lazy">
        </a>
        <div class="product-body">
          <p class="eyebrow">${esc(product.categoryName || "Ürün")}</p>
          <div class="product-title-row">
            <h3><a href="/Products/Detail/${product.id}">${esc(product.definition)}</a></h3>
            <div class="price">${toMoney(product.unitPrice)}</div>
          </div>
          <div class="product-bottom">
            <span class="stock-label">${Number(product.stockAmount || 0) > 0 ? "Stokta" : "Tükendi"}</span>
            <button class="btn btn-outline-primary product-add" type="button" data-add-cart="${product.id}" ${Number(product.stockAmount || 0) < 1 ? "disabled" : ""}>Sepete Ekle</button>
          </div>
        </div>
      </article>`;
  }

  async function initCategoryNavigation() {
    const host = qs("[data-category-nav]");
    if (!host) return;
    try {
      // Menü ve katalog aynı kategori kimliğini kullanır; isim değişse de bağlantı çalışır.
      const categories = await api("/api/categories");
      const selectedId = new URLSearchParams(window.location.search).get("categoryId") || "";
      const isCatalogue = window.location.pathname.toLowerCase() === "/products";
      host.innerHTML = `<a href="/Products" ${!selectedId && isCatalogue ? 'aria-current="page"' : ""}>Tüm Ürünler</a>`
        + categories.map((category) => `<a href="/Products?categoryId=${category.id}" ${String(category.id) === selectedId && isCatalogue ? 'aria-current="page"' : ""}>${esc(category.definition)}</a>`).join("");
      const office = categories.find((category) => category.definition.toLocaleLowerCase("tr-TR").includes("ofis"));
      if (office) qs("[data-office-link]")?.setAttribute("href", `/Products?categoryId=${office.id}`);
    } catch {
      // Kategori servisi yanıt vermezse tüm ürünler bağlantısı kullanılabilir kalır.
    }
  }

  async function initHome() {
    const host = qs("[data-featured-products]");
    if (!host) return;
    try {
      const data = await api("/api/products?page=1&pageSize=5&sort=newest");
      const items = data?.items || [];
      host.innerHTML = items.map(productCard).join("");
      qs("[data-featured-empty]").hidden = items.length > 0;
    } catch {
      qs("[data-featured-empty]").hidden = false;
    }
  }

  async function initProducts() {
    const state = { page: 1, pageSize: 12, search: "", categoryId: new URLSearchParams(window.location.search).get("categoryId") || "", sort: "newest" };
    const list = qs("[data-product-list]");
    const summary = qs("[data-product-summary]");
    const pager = qs("[data-product-pager]");
    const empty = qs("[data-product-empty]");
    const error = qs("[data-product-error]");
    let requestVersion = 0;

    async function loadCategories() {
      const select = qs("[data-category-filter]");
      const categories = await api("/api/categories");
      select.innerHTML = '<option value="">Tüm kategoriler</option>' + categories.map((c) => `<option value="${c.id}">${esc(c.definition)}</option>`).join("");
      select.value = state.categoryId;
    }

    async function loadProducts() {
      const version = ++requestVersion;
      try {
        error.hidden = true;
        list.innerHTML = '<div class="state-card">Ürünler yükleniyor...</div>';
        const params = new URLSearchParams({
          page: state.page,
          pageSize: state.pageSize,
          sort: state.sort
        });
        if (state.search) params.set("search", state.search);
        if (state.categoryId) params.set("categoryId", state.categoryId);
        const data = await api(`/api/products?${params}`);
        // Önceki aramanın geç gelen yanıtı yeni sonuçları değiştirmesin.
        if (version !== requestVersion) return;
        const items = data?.items || [];
        list.innerHTML = items.map(productCard).join("");
        empty.hidden = items.length > 0;
        summary.textContent = `${data?.totalCount ?? items.length} ürün`;
        renderPager(pager, data?.page || 1, data?.totalPages || 1, (next) => {
          state.page = next;
          loadProducts();
        });
      } catch {
        if (version !== requestVersion) return;
        list.innerHTML = "";
        error.hidden = false;
      }
    }

    await loadCategories().catch(() => {});
    qs("[data-product-search]")?.addEventListener("input", debounce((event) => {
      state.search = event.target.value.trim();
      state.page = 1;
      loadProducts();
    }, 250));
    qs("[data-category-filter]")?.addEventListener("change", (event) => {
      state.categoryId = event.target.value;
      state.page = 1;
      loadProducts();
    });
    qs("[data-sort-filter]")?.addEventListener("change", (event) => {
      state.sort = event.target.value;
      state.page = 1;
      loadProducts();
    });
    await loadProducts();
  }

  function renderPager(host, current, total, onPick) {
    host.innerHTML = "";
    for (let i = 1; i <= total; i += 1) {
      const button = document.createElement("button");
      button.type = "button";
      button.textContent = i;
      if (i === current) button.setAttribute("aria-current", "page");
      button.addEventListener("click", () => onPick(i));
      host.appendChild(button);
    }
  }

  async function initProductDetail() {
    const shell = qs("[data-page='product-detail']");
    if (!shell) return;
    try {
      const product = await api(`/api/products/${shell.dataset.productId}`);
      const photos = [...new Set([product.coverPhoto, ...(product.imagePaths || [])]
        .filter(Boolean).map((coverPhoto) => imageOf({ coverPhoto })))];
      if (!photos.length) photos.push(imageOf(null));
      shell.innerHTML = `
        <a class="back-link" href="/Products">← Kataloğa dön</a>
        <article class="detail-card">
          <section class="product-gallery" aria-label="${esc(product.definition)} görselleri" tabindex="0" data-product-gallery>
            <div class="gallery-main">
              <img class="gallery-image" src="${esc(photos[0])}" alt="${esc(product.definition)} · Görsel 1" data-gallery-image>
              <button class="gallery-arrow gallery-previous" type="button" aria-label="Önceki görsel" data-gallery-step="-1" ${photos.length < 2 ? "hidden" : ""}>&#8249;</button>
              <button class="gallery-arrow gallery-next" type="button" aria-label="Sonraki görsel" data-gallery-step="1" ${photos.length < 2 ? "hidden" : ""}>&#8250;</button>
            </div>
            <div class="gallery-thumbnails" aria-label="Görsel seçimi" ${photos.length < 2 ? "hidden" : ""}>
              ${photos.map((photo, index) => `<button class="gallery-thumbnail ${index === 0 ? "is-active" : ""}" type="button" data-gallery-index="${index}" aria-label="${index + 1}. görseli göster" aria-current="${index === 0 ? "true" : "false"}"><img src="${esc(photo)}" alt="" loading="lazy"></button>`).join("")}
            </div>
            <p class="gallery-counter muted" data-gallery-counter aria-live="polite" ${photos.length < 2 ? "hidden" : ""}>1 / ${photos.length}</p>
          </section>
          <div class="detail-title">
            <p class="eyebrow">${esc(product.categoryName || "Ürün")}</p>
            <h1>${esc(product.definition)}</h1>
            <p class="muted">${esc(product.description || "")}</p>
            <div class="price">${toMoney(product.unitPrice)}</div>
            <p class="stock-label">${Number(product.stockAmount || 0) > 0 ? `${product.stockAmount} adet stokta` : "Stokta yok"}</p>
            <button class="primary-action" type="button" data-add-cart="${product.id}" ${Number(product.stockAmount || 0) < 1 ? "disabled" : ""}>Sepete ekle</button>
            <div class="product-specs"><p>Ürün kodu: ${esc(product.barcode || "-")}</p><p>KDV dahil fiyat · KDV oranı %${esc(product.vatRate ?? 0)}</p><p>KDV hariç ${toMoney(product.price)}</p></div>
          </div>
        </article>`;
      initProductGallery(qs("[data-product-gallery]", shell), photos, product.definition);
    } catch (error) {
      shell.innerHTML = `<div class="state-card error">${esc(error.message)}</div>`;
    }
  }

  function initProductGallery(gallery, photos, name) {
    const image = qs("[data-gallery-image]", gallery);
    const thumbnails = qsa("[data-gallery-index]", gallery);
    let selected = 0;
    let touchStart = null;
    function selectPhoto(index) {
      selected = (index + photos.length) % photos.length;
      image.src = photos[selected];
      image.alt = `${name} · Görsel ${selected + 1}`;
      qs("[data-gallery-counter]", gallery).textContent = `${selected + 1} / ${photos.length}`;
      thumbnails.forEach((button, position) => {
        button.classList.toggle("is-active", position === selected);
        button.setAttribute("aria-current", String(position === selected));
      });
      // Küçük görseller kendi şeridinde kayar; sayfanın yatay taşmasına yol açmaz.
      const strip = qs(".gallery-thumbnails", gallery);
      const active = thumbnails[selected];
      strip.scrollTo({ left: active.offsetLeft - strip.offsetLeft - (strip.clientWidth - active.clientWidth) / 2, behavior: "smooth" });
    }
    gallery.addEventListener("click", (event) => {
      const thumbnail = event.target.closest("[data-gallery-index]");
      const arrow = event.target.closest("[data-gallery-step]");
      if (thumbnail) selectPhoto(Number(thumbnail.dataset.galleryIndex));
      if (arrow) selectPhoto(selected + Number(arrow.dataset.galleryStep));
    });
    gallery.addEventListener("keydown", (event) => {
      if (photos.length < 2 || !["ArrowLeft", "ArrowRight"].includes(event.key)) return;
      event.preventDefault();
      selectPhoto(selected + (event.key === "ArrowRight" ? 1 : -1));
    });
    const main = qs(".gallery-main", gallery);
    main.addEventListener("pointerdown", (event) => {
      if (event.pointerType !== "mouse") touchStart = { x: event.clientX, y: event.clientY };
    });
    main.addEventListener("pointerup", (event) => {
      if (!touchStart || photos.length < 2) return;
      const dx = event.clientX - touchStart.x;
      const dy = event.clientY - touchStart.y;
      touchStart = null;
      // Dikey sayfa kaydırmasını koruyup yalnızca belirgin yatay hareketi görsel geçişi sayıyoruz.
      if (Math.abs(dx) >= 40 && Math.abs(dx) > Math.abs(dy) * 1.2) selectPhoto(selected + (dx < 0 ? 1 : -1));
    });
    main.addEventListener("pointercancel", () => { touchStart = null; });
    qsa("img", gallery).forEach((photo) => photo.addEventListener("error", () => {
      if (!photo.src.endsWith("/images/product-placeholder.svg")) photo.src = "/images/product-placeholder.svg";
    }));
  }

  async function initCart() {
    const host = qs("[data-cart-lines]");
    if (!host) return;
    const empty = qs("[data-cart-empty]");
    const error = qs("[data-cart-error]");
    const message = qs("[data-checkout-message]");
    const checkoutForm = qs("[data-checkout-form]");
    const checkoutButton = qs("[data-checkout-button]");
    let checkoutBlocked = false;
    let checkoutPending = false;
    let cartLoading = false;
    let totalCartQuantity = 0;
    let cartRequestVersion = 0;
    const lockedControls = new Map();

    function updateCheckoutButton() {
      checkoutButton.disabled = checkoutPending || cartLoading || checkoutBlocked || totalCartQuantity === 0;
    }

    function setCheckoutPending(pending) {
      checkoutPending = pending;
      // Adet ve adres gönderim boyunca sabit kalır. Önceden kapalı olan kontroller
      // hata sonrasında da kapalı kalır; yalnızca bu işlem için kilitlenenler açılır.
      if (pending) {
        [...qsa("input,button", host), ...qsa("input,textarea,select,button", checkoutForm)].forEach((control) => {
          if (!lockedControls.has(control)) lockedControls.set(control, control.disabled);
          control.disabled = true;
        });
      } else {
        lockedControls.forEach((disabled, control) => { control.disabled = disabled; });
        lockedControls.clear();
      }
      checkoutForm.setAttribute("aria-busy", String(pending));
      updateCheckoutButton();
    }

    async function loadCart() {
      const version = ++cartRequestVersion;
      cartLoading = true;
      updateCheckoutButton();
      const lines = getCart();
      try {
        if (!lines.length) {
          checkoutBlocked = false;
          host.innerHTML = "";
          empty.hidden = false;
          error.hidden = true;
          updateSummary([]);
          return [];
        }
        error.hidden = true;
        empty.hidden = true;
        host.innerHTML = '<div class="state-card">Sepet güncelleniyor...</div>';
        const results = await Promise.allSettled(lines.map((line) => api(`/api/products/${line.productId}`).then((product) => ({ ...line, product }))));
        if (version !== cartRequestVersion) return [];
        const transientError = results.find((result) => result.status === "rejected" && result.reason?.status !== 404);
        if (transientError) throw transientError.reason;
        const liveLines = results.filter((result) => result.status === "fulfilled").map((result) => result.value);
        const unavailableLines = lines.filter((line, index) => results[index].status === "rejected");
        const invalidQuantity = liveLines.some(({ product, quantity }) => !Number.isInteger(Number(quantity))
          || Number(quantity) < 1 || Number(quantity) > Math.min(product.stockAmount, maximumOrderQuantity));
        checkoutBlocked = unavailableLines.length > 0 || invalidQuantity;
        if (invalidQuantity) {
          error.hidden = false;
          error.textContent = "Sepetteki ürünlerin adetlerini stok ve sipariş sınırına göre düzenleyin.";
        }
        host.innerHTML = liveLines.map(({ product, quantity }) => `
          <article class="cart-line">
            <img src="${esc(imageOf(product))}" alt="${esc(product.definition)}">
            <div>
              <p class="muted">${esc(product.categoryName || "")}</p>
              <h3><a href="/Products/Detail/${product.id}">${esc(product.definition)}</a></h3>
              <div class="unit-price">${toMoney(product.unitPrice)} / adet</div>
            </div>
            <div class="line-actions">
              <div class="quantity-control">
                <button type="button" data-cart-adjust="${product.id}" data-step="-1" aria-label="${esc(product.definition)} adet azalt">−</button>
                <input aria-label="${esc(product.definition)} adedi" type="number" min="1" max="${Math.min(product.stockAmount, maximumOrderQuantity)}" value="${esc(quantity)}" data-cart-qty="${product.id}">
                <button type="button" data-cart-adjust="${product.id}" data-step="1" aria-label="${esc(product.definition)} adet artır" ${quantity >= Math.min(product.stockAmount, maximumOrderQuantity) ? "disabled" : ""}>+</button>
              </div>
              <div class="price">${toMoney(product.unitPrice * quantity)}</div>
              <button class="remove-item" type="button" data-remove-cart="${product.id}">Kaldır</button>
            </div>
          </article>`).join("") + unavailableLines.map((line) => `
          <article class="cart-line">
            <img src="/images/product-placeholder.svg" alt="Ürün bulunamadı">
            <div>
              <h3>Ürün bulunamadı</h3>
              <p class="muted">Bu ürün artık katalogda yok. Siparişe devam etmek için sepetten kaldırın.</p>
            </div>
            <div class="line-actions">
              <span class="status-pill">${esc(line.quantity)} adet</span>
              <button class="remove-item" type="button" data-remove-cart="${line.productId}">Kaldır</button>
            </div>
          </article>`).join("");
        updateSummary(liveLines);
        return liveLines;
      } catch (loadError) {
        if (version !== cartRequestVersion) return [];
        checkoutBlocked = true;
        host.innerHTML = "";
        error.hidden = false;
        error.textContent = loadError?.message || "Sepet bilgileri alınamadı.";
        updateSummary([]);
        return [];
      } finally {
        if (version === cartRequestVersion) {
          cartLoading = false;
          if (checkoutPending) setCheckoutPending(true);
          updateCheckoutButton();
        }
      }
    }

    function updateSummary(lines) {
      const totalQuantity = lines.reduce((sum, line) => sum + Number(line.quantity || 0), 0);
      totalCartQuantity = totalQuantity;
      const total = lines.reduce((sum, line) => sum + Number(line.quantity || 0) * Number(line.product?.unitPrice || 0), 0);
      qs("[data-cart-quantity]").textContent = totalQuantity;
      qs("[data-cart-total]").textContent = toMoney(total);
      updateCheckoutButton();
    }

    host.addEventListener("change", (event) => {
      if (checkoutPending || cartLoading) return;
      const id = Number(event.target.dataset.cartQty);
      if (!id) return;
      const quantity = Number(event.target.value || 1);
      if (!Number.isInteger(quantity) || quantity > Number(event.target.max)) {
        showToast(`Bu ürün için en fazla ${event.target.max} adet girebilirsiniz. Adet tam sayı olmalıdır.`);
        event.target.value = getCart().find((line) => line.productId === id)?.quantity || 1;
        return;
      }
      updateCartLine(id, quantity);
      loadCart();
    });
    host.addEventListener("click", (event) => {
      if (checkoutPending || cartLoading) return;
      const adjustment = event.target.closest("[data-cart-adjust]");
      if (adjustment) {
        const id = Number(adjustment.dataset.cartAdjust);
        const input = qs(`[data-cart-qty="${id}"]`, host);
        const quantity = Number(input.value) + Number(adjustment.dataset.step);
        if (quantity >= 0 && (quantity <= Number(input.max) || quantity < Number(input.value))) {
          updateCartLine(id, quantity);
          loadCart().then(() => {
            qs(`[data-cart-adjust="${id}"][data-step="${adjustment.dataset.step}"]`, host)?.focus({ preventScroll: true });
          });
        }
        return;
      }
      const id = Number(event.target.dataset.removeCart);
      if (!id) return;
      updateCartLine(id, 0);
      loadCart();
    });
    checkoutForm.addEventListener("submit", async (event) => {
      event.preventDefault();
      if (checkoutPending || cartLoading) return;
      const address = new FormData(event.currentTarget).get("deliveryAddress")?.toString().trim();
      const lines = getCart();
      if (!currentUser) {
        window.location.href = `/Account/Login?returnUrl=${encodeURIComponent("/Cart")}`;
        return;
      }
      if (!address || address.length < 10 || !lines.length) return;
      if (checkoutBlocked) {
        message.textContent = "Sepetteki ürünlerin stok ve adet bilgilerini kontrol edin.";
        message.classList.add("error");
        return;
      }
      setCheckoutPending(true);
      let completed = false;
      message.textContent = "Sipariş oluşturuluyor...";
      message.classList.remove("error");
      try {
        const requestId = getCheckoutRequestId(address, lines);
        const order = await api("/api/orders", {
          method: "POST",
          body: JSON.stringify({
            deliveryAddress: address,
            requestId,
            orderLines: lines.map((line) => ({ productId: line.productId, quantity: line.quantity }))
          })
        });
        // Başka bir sekmede eklenen ürünler kaybolmasın; yalnızca gönderilen adetler düşülür.
        const remaining = getCart().map((line) => ({ ...line,
          quantity: Number(line.quantity) - Number(lines.find((submitted) => submitted.productId === line.productId)?.quantity || 0)
        })).filter((line) => line.quantity > 0);
        setCart(remaining);
        sessionStorage.removeItem(checkoutRequestKey);
        completed = true;
        window.location.href = `/Orders/Detail/${order.id}`;
      } catch (error) {
        message.textContent = error.message;
        message.classList.add("error");
      } finally {
        if (!completed) setCheckoutPending(false);
      }
    });
    await loadCart();
  }

  async function initAuth(mode) {
    const form = qs(mode === "login" ? "[data-login-form]" : "[data-register-form]");
    if (!form) return;
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const message = qs("[data-auth-message]");
      message.textContent = "İşlem yapılıyor...";
      message.classList.remove("error");
      const payload = Object.fromEntries(new FormData(form).entries());
      try {
        await api(`/api/auth/${mode}`, { method: "POST", body: JSON.stringify(payload) });
        await refreshCsrf();
        window.location.href = safeLocalUrl(qs("[data-return-url]")?.dataset.returnUrl);
      } catch (error) {
        message.textContent = error.message;
        message.classList.add("error");
      }
    });
  }

  async function initOrders() {
    const host = qs("[data-orders-list]");
    if (!host) return;
    try {
      const orders = await api("/api/orders");
      host.innerHTML = orders.map(orderRow).join("");
      qs("[data-orders-empty]").hidden = orders.length > 0;
    } catch {
      qs("[data-orders-error]").hidden = false;
    }
  }

  function orderRow(order) {
    const canCancel = order.status === "Preparing";
    return `<tr>
      <td>#${order.id}</td>
      <td>${esc(toDate(order.orderDate))}</td>
      <td><span class="status-pill">${esc(statusText(order.status))}</span></td>
      <td>${toMoney(order.totalAmount)}</td>
      <td class="status-row">
        <a class="mini-button" href="/Orders/Detail/${order.id}">Detay</a>
        ${canCancel ? `<button class="danger-button" type="button" data-cancel-order="${order.id}">İptal et</button>` : ""}
      </td>
    </tr>`;
  }

  async function initOrderDetail() {
    const shell = qs("[data-page='order-detail']");
    if (!shell) return;
    try {
      const order = await api(`/api/orders/${shell.dataset.orderId}`);
      shell.innerHTML = orderDetail(order);
    } catch (error) {
      shell.innerHTML = `<div class="state-card error">${esc(error.message)}</div>`;
    }
  }

  function orderDetail(order) {
    return `<a class="back-link" href="/Orders">← Siparişlerime dön</a><article class="detail-card order-detail">
      <div>
        <p class="eyebrow">Sipariş #${order.id}</p>
        <h1>${esc(statusText(order.status))}</h1>
        <p>${esc(toDate(order.orderDate))}</p>
        <p>${esc(order.deliveryAddress)}</p>
        <div class="price">${toMoney(order.totalAmount)}</div>
      </div>
      <div class="table-wrap">
        <table class="table data-table">
          <thead><tr><th>Ürün</th><th>Adet</th><th>Birim</th><th>Toplam</th></tr></thead>
          <tbody>${(order.orderLines || []).map((line) => `<tr><td>${esc(line.productName)}</td><td>${line.quantity}</td><td>${toMoney(line.unitPrice)}</td><td>${toMoney(line.lineTotalAmount)}</td></tr>`).join("")}</tbody>
        </table>
      </div>
    </article>`;
  }

  async function cancelOrder(id) {
    try {
      await api(`/api/orders/${id}/cancel`, { method: "POST" });
      window.location.reload();
    } catch (error) {
      showToast(error.message);
    }
  }

  async function initDashboard() {
    const metrics = qs("[data-dashboard-metrics]");
    if (!metrics) return;
    try {
      const data = await api("/api/admin/dashboard");
      const metricItems = [
        ["Ürün", data.productCount],
        ["Kategori", data.categoryCount],
        ["Müşteri", data.customerCount],
        ["Sipariş", data.orderCount],
        ["Gelir", toMoney(data.totalRevenue)]
      ];
      metrics.innerHTML = metricItems.map(([label, value]) => `<div class="metric-card"><span>${esc(label)}</span><strong>${esc(value)}</strong></div>`).join("");
      qs("[data-low-stock]").innerHTML = smallProductList(data.lowStockProducts || []);
      qs("[data-recent-orders]").innerHTML = (data.recentOrders || []).map((order) => `<a class="admin-list-row" href="/Orders/Detail/${order.id}"><span>#${order.id} · ${esc(order.customerName)}</span><span class="amount">${toMoney(order.totalAmount)}</span><small>${esc(statusText(order.status))}</small></a>`).join("") || "<p class='muted'>Henüz sipariş yok.</p>";
    } catch (error) {
      metrics.innerHTML = `<div class="state-card error">${esc(error.message)}</div>`;
    }
  }

  function smallProductList(items) {
    return items.length
      ? items.map((p) => `<p>${esc(p.definition)} · ${esc(p.stockAmount)} stok</p>`).join("")
      : "<p class='muted'>Düşük stok ürünü yok.</p>";
  }

  async function initAdminProducts() {
    const host = qs("[data-admin-products-list]");
    if (!host) return;
    const form = qs("[data-product-form]");
    const pager = qs("[data-admin-product-pager]");
    const state = { page: 1, pageSize: 10, search: "" };
    let requestVersion = 0;
    async function load() {
      const version = ++requestVersion;
      const params = new URLSearchParams({ page: state.page, pageSize: state.pageSize, sort: "newest" });
      if (state.search) params.set("search", state.search);
      try {
        const [categories, data] = await Promise.all([api("/api/admin/categories"), api(`/api/admin/products?${params}`)]);
        if (version !== requestVersion) return;
        // Arama yalnızca listeyi yeniler; açık formda yöneticinin seçtiği kategori korunur.
        const selectedCategory = form.elements.categoryId.value;
        form.elements.categoryId.innerHTML = categories.map((c) => `<option value="${c.id}">${esc(c.definition)}</option>`).join("");
        form.elements.categoryId.value = selectedCategory;
        host.innerHTML = (data.items || []).map((p) => `<tr>
        <td>${esc(p.definition)}</td><td>${esc(p.categoryName)}</td><td>${toMoney(p.unitPrice)}</td><td>${esc(p.stockAmount)}</td><td>${p.isActive ? "Aktif" : "Pasif"}</td>
        <td class="status-row"><button class="mini-button" data-edit-product="${p.id}" type="button">Düzenle</button><button class="danger-button" data-delete-product="${p.id}" type="button">Sil</button></td>
      </tr>`).join("");
        host.dataset.products = JSON.stringify(data.items || []);
        renderPager(pager, data.page || 1, data.totalPages || 1, (next) => {
          state.page = next;
          refreshList();
        });
      } catch (error) {
        if (version === requestVersion) throw error;
      }
    }
    function refreshList() {
      return load().catch((error) => showAdminMessage(form, error.message, true));
    }
    qs("[data-admin-product-search]")?.addEventListener("input", debounce((event) => {
      state.search = event.target.value.trim();
      state.page = 1;
      refreshList();
    }, 250));
    qs("[data-new-product]")?.addEventListener("click", () => showProductForm(form));
    const photoList = qs("[data-product-photo-list]", form);
    const addPhoto = qs("[data-add-product-photo]", form);
    addPhoto.addEventListener("click", () => {
      if (photoList.children.length >= 10) return;
      appendProductPhoto(form);
      qs("input", photoList.lastElementChild).focus();
    });
    photoList.addEventListener("click", (event) => {
      const remove = event.target.closest("[data-remove-product-photo]");
      if (!remove) return;
      remove.closest(".product-photo-row").remove();
      addPhoto.disabled = photoList.children.length >= 10;
      addPhoto.focus();
    });
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const payload = formPayload(form, ["categoryId", "price", "vatRate", "stockAmount"]);
      payload.imagePaths = qsa("[data-product-photo-path]", form).map((input) => input.value.trim()).filter(Boolean);
      const id = form.elements.id.value;
      if (id) payload.originalStockAmount = Number(form.dataset.originalStockAmount);
      const submitButton = qs("button[type='submit']", form);
      submitButton.disabled = true;
      showAdminMessage(form, "");
      try {
        await api(id ? `/api/admin/products/${id}` : "/api/admin/products", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) });
        form.hidden = true;
        showAdminMessage(form, "Ürün kaydedildi.");
        try {
          await load();
        } catch {
          showAdminMessage(form, "Ürün kaydedildi, ancak liste yenilenemedi. Sayfayı yenileyin.", true);
        }
      } catch (error) {
        showAdminMessage(form, error.message, true);
      } finally {
        submitButton.disabled = false;
      }
    });
    form.querySelector("[data-cancel-form]")?.addEventListener("click", () => { form.hidden = true; });
    host.addEventListener("click", async (event) => {
      const editId = Number(event.target.dataset.editProduct);
      const deleteId = Number(event.target.dataset.deleteProduct);
      const products = JSON.parse(host.dataset.products || "[]");
      if (editId) showProductForm(form, products.find((p) => p.id === editId));
      if (deleteId) {
        try {
          await api(`/api/admin/products/${deleteId}`, { method: "DELETE" });
          await load();
        } catch (error) {
          showToast(error.message);
        }
      }
    });
    await refreshList();
  }

  function showProductForm(form, product = null) {
    form.hidden = false;
    form.reset();
    showAdminMessage(form, "");
    form.dataset.originalStockAmount = product ? String(product.stockAmount) : "";
    form.elements.id.value = product?.id || "";
    form.elements.definition.value = product?.definition || "";
    form.elements.categoryId.value = product?.categoryId || "";
    form.elements.barcode.value = product?.barcode || "";
    form.elements.coverPhoto.value = product?.coverPhoto || "";
    qs("[data-product-photo-list]", form).replaceChildren();
    qs("[data-add-product-photo]", form).disabled = false;
    (product?.imagePaths || []).forEach((path) => appendProductPhoto(form, path));
    form.elements.price.value = product?.price || "";
    // Kayıtlı %0 oranını korur; yeni üründe KDV oranını yönetici belirler.
    form.elements.vatRate.value = product?.vatRate ?? "";
    form.elements.stockAmount.value = product?.stockAmount ?? 10;
    form.elements.description.value = product?.description || "";
    form.elements.isActive.checked = product?.isActive ?? true;
    form.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  function appendProductPhoto(form, path = "") {
    const list = qs("[data-product-photo-list]", form);
    const row = document.createElement("div");
    row.className = "product-photo-row";
    row.innerHTML = `<img class="product-photo-preview" src="/images/product-placeholder.svg" alt="Ek görsel önizlemesi"><label>Görsel adresi<input class="form-control" data-product-photo-path value="${esc(path)}" maxlength="400" placeholder="/images/products/urun-detay.png" aria-describedby="product-photo-help" required></label><button class="mini-button" type="button" data-remove-product-photo>Kaldır</button>`;
    const input = qs("input", row);
    const preview = qs("img", row);
    function updatePreview() {
      const value = input.value.trim();
      // Önizleme yalnızca uygulamanın desteklediği görsel adreslerini yükler.
      const local = /^\/images\/(?:[\p{L}\p{N}_.-]+\/)*[\p{L}\p{N}_.-]+$/u.test(value)
        && !value.split("/").some((part) => part === "." || part === "..");
      let https = false;
      try { const url = new URL(value); https = url.protocol === "https:" && !url.username && !url.password; } catch { /* Yerel dosya yolu olabilir. */ }
      preview.src = local || https ? imageOf({ coverPhoto: value }) : "/images/product-placeholder.svg";
    }
    input.addEventListener("change", updatePreview);
    preview.addEventListener("error", () => {
      if (!preview.src.endsWith("/images/product-placeholder.svg")) preview.src = "/images/product-placeholder.svg";
    });
    list.appendChild(row);
    updatePreview();
    qs("[data-add-product-photo]", form).disabled = list.children.length >= 10;
  }

  async function initAdminCategories() {
    const host = qs("[data-admin-categories-list]");
    if (!host) return;
    const form = qs("[data-category-form]");
    async function load() {
      const categories = await api("/api/admin/categories");
      host.dataset.categories = JSON.stringify(categories);
      host.innerHTML = categories.map((c) => `<tr>
        <td>${esc(c.definition)}</td><td>${esc(c.description || "")}</td><td>${c.isActive ? "Aktif" : "Pasif"}</td>
        <td class="status-row"><button class="mini-button" data-edit-category="${c.id}" type="button">Düzenle</button><button class="danger-button" data-delete-category="${c.id}" type="button">Sil</button></td>
      </tr>`).join("");
    }
    qs("[data-new-category]")?.addEventListener("click", () => showCategoryForm(form));
    form.addEventListener("submit", async (event) => {
      event.preventDefault();
      const payload = formPayload(form);
      const id = form.elements.id.value;
      const submitButton = qs("button[type='submit']", form);
      submitButton.disabled = true;
      showAdminMessage(form, "");
      try {
        await api(id ? `/api/admin/categories/${id}` : "/api/admin/categories", { method: id ? "PUT" : "POST", body: JSON.stringify(payload) });
        form.hidden = true;
        showAdminMessage(form, "Kategori kaydedildi.");
        try {
          await load();
        } catch {
          showAdminMessage(form, "Kategori kaydedildi, ancak liste yenilenemedi. Sayfayı yenileyin.", true);
        }
      } catch (error) {
        showAdminMessage(form, error.message, true);
      } finally {
        submitButton.disabled = false;
      }
    });
    form.querySelector("[data-cancel-form]")?.addEventListener("click", () => { form.hidden = true; });
    host.addEventListener("click", async (event) => {
      const editId = Number(event.target.dataset.editCategory);
      const deleteId = Number(event.target.dataset.deleteCategory);
      const categories = JSON.parse(host.dataset.categories || "[]");
      if (editId) showCategoryForm(form, categories.find((c) => c.id === editId));
      if (deleteId) {
        try {
          await api(`/api/admin/categories/${deleteId}`, { method: "DELETE" });
          await load();
        } catch (error) {
          showToast(error.message);
        }
      }
    });
    await load();
  }

  function showCategoryForm(form, category = null) {
    form.hidden = false;
    form.reset();
    showAdminMessage(form, "");
    form.elements.id.value = category?.id || "";
    form.elements.definition.value = category?.definition || "";
    form.elements.description.value = category?.description || "";
    form.elements.isActive.checked = category?.isActive ?? true;
    form.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  async function initAdminOrders() {
    const host = qs("[data-admin-orders-list]");
    if (!host) return;
    let updating = false;
    async function loadOrders() {
      try {
        const orders = await api("/api/admin/orders");
        host.innerHTML = orders.map((order) => `<tr>
        <td>#${order.id}</td><td>${esc(order.customerName)}<br><span class="muted">${esc(order.customerEmail)}</span></td><td>${esc(toDate(order.orderDate))}</td>
        <td><span class="status-pill">${esc(statusText(order.status))}</span></td><td>${toMoney(order.totalAmount)}</td>
        <td><a class="mini-button" href="/Orders/Detail/${order.id}">Detay</a><select data-order-status="${order.id}" aria-label="Sipariş #${order.id} durumu" ${["Completed", "Canceled"].includes(order.status) ? "disabled" : ""}>${(order.status === "Preparing" ? ["Preparing", "Shipped", "Canceled"] : order.status === "Shipped" ? ["Shipped", "Completed"] : [order.status]).map((s) => `<option value="${s}" ${s === order.status ? "selected" : ""}>${statusText(s)}</option>`).join("")}</select></td>
      </tr>`).join("");
      } catch (error) {
        host.innerHTML = `<tr><td colspan="6" class="form-note error">${esc(error.message)}</td></tr>`;
      }
    }
    // Tablo yenilense veya istek hata verse de olay dinleyicisi bağlı kalır.
    host.addEventListener("change", async (event) => {
      const id = Number(event.target.dataset.orderStatus);
      if (!id || updating) return;
      updating = true;
      qsa("[data-order-status]", host).forEach((select) => { select.disabled = true; });
      try {
        await api(`/api/admin/orders/${id}/status`, { method: "PUT", body: JSON.stringify({ status: event.target.value }) });
      } catch (error) {
        showToast(error.message);
      } finally {
        await loadOrders();
        updating = false;
      }
    });
    await loadOrders();
  }

  async function initAdminUsers() {
    const host = qs("[data-admin-users-list]");
    if (!host) return;
    const users = await api("/api/admin/users");
    host.innerHTML = users.map((user) => `<tr><td>${esc(user.name)} ${esc(user.surname)}</td><td>${esc(user.email)}</td><td>${user.role === "Manager" ? "Yönetici" : "Müşteri"}</td></tr>`).join("");
  }

  function formPayload(form, numericFields = []) {
    const data = Object.fromEntries(new FormData(form).entries());
    qsa("input[type='checkbox']", form).forEach((input) => { data[input.name] = input.checked; });
    numericFields.forEach((field) => { data[field] = Number(data[field] || 0); });
    delete data.id;
    return data;
  }

  function getCheckoutRequestId(address, lines) {
    const fingerprint = JSON.stringify({
      deliveryAddress: address,
      orderLines: lines
        .map((line) => ({ productId: Number(line.productId), quantity: Number(line.quantity) }))
        .sort((a, b) => a.productId - b.productId)
    });
    try {
      const saved = JSON.parse(sessionStorage.getItem(checkoutRequestKey) || "null");
      if (saved?.fingerprint === fingerprint && saved?.requestId) return saved.requestId;
    } catch {
      sessionStorage.removeItem(checkoutRequestKey);
    }
    const requestId = crypto.randomUUID();
    sessionStorage.setItem(checkoutRequestKey, JSON.stringify({ fingerprint, requestId }));
    return requestId;
  }

  function showAdminMessage(form, message, isError = false) {
    // Bildirim formun dışında kalır; form kapatıldığında da kullanıcı görür.
    const target = qs("[data-admin-message]", form.closest(".admin-content"));
    if (!target) return;
    target.textContent = message;
    target.classList.toggle("error", isError);
    target.hidden = !message;
  }

  function showToast(message) {
    let toast = qs("[data-toast]");
    if (!toast) {
      toast = document.createElement("div");
      toast.className = "toast-message";
      toast.dataset.toast = "";
      toast.setAttribute("role", "status");
      document.body.appendChild(toast);
    }
    toast.textContent = message;
    toast.hidden = false;
    clearTimeout(showToast.timer);
    showToast.timer = setTimeout(() => { toast.hidden = true; }, 4200);
  }

  function statusText(status) {
    return {
      Preparing: "Hazırlanıyor",
      Shipped: "Kargoda",
      Completed: "Tamamlandı",
      Canceled: "İptal"
    }[status] || status || "-";
  }

  function debounce(fn, delay) {
    let timer;
    return (...args) => {
      clearTimeout(timer);
      timer = setTimeout(() => fn(...args), delay);
    };
  }

  document.addEventListener("click", (event) => {
    const productId = Number(event.target.dataset.addCart);
    const cancelId = Number(event.target.dataset.cancelOrder);
    if (productId && addToCart(productId)) {
      event.target.textContent = "Sepete eklendi";
      setTimeout(() => { event.target.textContent = "Sepete Ekle"; }, 1200);
    }
    if (cancelId) cancelOrder(cancelId);
  });

  document.addEventListener("error", (event) => {
    const target = event.target;
    if (target instanceof HTMLImageElement && !target.dataset.fallbackApplied) {
      target.dataset.fallbackApplied = "true";
      target.src = "/images/product-placeholder.svg";
    }
  }, true);

  document.addEventListener("DOMContentLoaded", async () => {
    qsa(".nav-links a").forEach((link) => {
      const path = new URL(link.href).pathname;
      if (window.location.pathname === path || window.location.pathname.startsWith(`${path}/`)) {
        link.setAttribute("aria-current", "page");
      }
    });
    qs("[data-menu-toggle]")?.addEventListener("click", (event) => {
      const open = event.currentTarget.getAttribute("aria-expanded") !== "true";
      event.currentTarget.setAttribute("aria-expanded", open ? "true" : "false");
      qsa("[data-menu], [data-account-area]").forEach((node) => node.classList.toggle("open", open));
    });

    renderCartCount();
    await initCategoryNavigation();
    await refreshCsrf();
    await refreshUser();

    const currentPage = page();
    if (currentPage === "home") await initHome();
    if (currentPage === "products") await initProducts();
    if (currentPage === "product-detail") await initProductDetail();
    if (currentPage === "cart") await initCart();
    if (currentPage === "login") await initAuth("login");
    if (currentPage === "register") await initAuth("register");
    if (currentPage === "orders") await initOrders();
    if (currentPage === "order-detail") await initOrderDetail();
    if (currentPage === "admin-dashboard") await initDashboard();
    if (currentPage === "admin-products") await initAdminProducts();
    if (currentPage === "admin-categories") await initAdminCategories();
    if (currentPage === "admin-orders") await initAdminOrders();
    if (currentPage === "admin-users") await initAdminUsers();
  });
})();
