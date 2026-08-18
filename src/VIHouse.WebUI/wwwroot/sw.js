// Minimal, conservative service worker (brief §65 — PWA / "Add to Home Screen").
//
// This exists to make the site installable, not to make it an offline app. It ONLY caches the
// fingerprinted static assets under /dist/ (main.css / main.js, versioned via asp-append-version's
// content-hash query string) — it never touches HTML pages, /checkout, /webhooks, /Admin, or
// /Identity, and never intercepts anything but GET. Caching an HTML page would mean serving a
// stale antiforgery token that fails on the next POST; on a payment-processing site that's a real
// correctness risk, not just unnecessary scope.
//
// Cache entries are keyed by the full request URL, including the "?v=" content-hash query string
// that asp-append-version already adds — so a rebuild that changes a file's content automatically
// produces a new URL (cache miss -> fetch -> cache), with no manual versioning to maintain here.

const CACHE_NAME = 'vih-static-v1';

self.addEventListener('install', () => {
  self.skipWaiting();
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((key) => key !== CACHE_NAME).map((key) => caches.delete(key)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const { request } = event;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin || !url.pathname.startsWith('/dist/')) return;

  event.respondWith(
    caches.open(CACHE_NAME).then(async (cache) => {
      const cached = await cache.match(request);
      if (cached) return cached;

      const response = await fetch(request);
      if (response.ok) cache.put(request, response.clone());
      return response;
    })
  );
});
