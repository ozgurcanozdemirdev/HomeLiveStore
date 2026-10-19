# SDD ledger — plan: docs/superpowers/plans/2026-09-11-store.md

Ruling: Kullanıcının özerk uygulama talimatı ve basit sipariş açıklaması kapsamında plan onay beklemeden uygulanır. Git deposu yok; worktree/commit yerine mevcut klasörde dosya sahipliği kullanılır.

| Lanes | Interface/shared files | Review |
|---|---|---|
| Backend / UI | Spec HTTP contract | camelCase, unitPrice KDV dahil, rol Manager, CSRF header sabit |
| Backend / Integration | Host registration | Backend host extension imzalarını lidere bildirir; Web Program liderde |
| UI / Integration | Web csproj/Program | UI bu dosyaları düzenlemez; lider Web'i API assembly'ye bağlar |
| Backend | Existing .NET 10/SQLite | Yeni paket yok; mevcut eksik repository ve auth onarılır |
| UI | MVC + vanilla JS | Dış build zinciri yok; gerçek API tüketilir |
| Integration | Console HTTP tests | Test framework bağımlılığı yok, gerçek host/SQLite doğrulanır |

Baseline: dotnet build başarılı, GenericRepository kaynaklı 3 uyarı. Runtime işlevleri yok; hava durumu ve MVC başlangıç sayfası var.

RED evidence: tests/IntegrationChecks --baseline returned three expected failures: /api/categories 404, /Products 404, /api/admin/dashboard 404. Backend/UI implementation lanes active.

Official reference checks: Microsoft .NET10 cookie/API behavior, antiforgery refresh, MVC ApplicationPart, EF/SQLite transactions and decimal restrictions verified by docs_check. API functions use cookies/CSRF; no external auth dependencies.

Integration green: scripts/verify.ps1 passed 3 domain + 31 HTTP checks, solution build 0 warnings/0 errors. Logs: App_Data/checks/7cbc43a2177c43518d50ee75df117d79.

Review fixes: removed price-snapshot inference, added unauthorized login status, corrected SQLite date ordering/detached entity behavior, category availability, seed asset paths. UI fix round covered local redirects, checkout retry idempotency, unavailable cart lines, JSON/error handling, async error surfaces and admin pagination.

Publish: Web Release publish initially exposed duplicate API/Web appsettings. Web now excludes only API host settings at publish after resolved absolute path normalization; Release publish succeeded. No checks suppressed.

Task 1: complete — backend contracts, domain, persistence, auth and order behavior verified.
Task 2: complete — desktop/mobile storefront and admin CRUD/order flows verified in Playwright; navigation collision fixed with regression.
Task 3: complete — final 3 domain +36 HTTP checks passed, Web/API publish successful, independent review APPROVE. Full evidence in verification.md.

Ruling: Reviewer concurrency finding was not reproducible and contradicted pinned Microsoft.Data.Sqlite10.0.12 BEGIN IMMEDIATE source; 8 simultaneous same-key requests all succeeded with one order and stock deduction. Default AddProblemDetails/status middleware already returned JSON on401/403/404; regressions prove this. Numeric enum and image boundary findings reproduced then fixed. No unresolved actionable findings.

Final local data: default development DB includes seeded catalogue/demo accounts and one browser demonstration order. Temporary browser product was soft-deleted. Test runs remain isolated under App_Data/checks. No Git repository/commit.
