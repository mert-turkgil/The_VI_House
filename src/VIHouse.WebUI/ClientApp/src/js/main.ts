import '../scss/main.scss';
import { initNav, initDismissableDropdown } from './modules/nav';

document.addEventListener('DOMContentLoaded', () => {
  initNav();
  initDismissableDropdown('.lang-switch');
  initDismissableDropdown('.notif-bell');
});

// PWA (brief §65) — registered on every page (Admin included, harmlessly; the service worker
// itself only ever caches /dist/*, see wwwroot/sw.js). Safe in Development too: cache entries are
// keyed by the full asp-append-version URL, so a rebuild's new content hash is a cache miss, not
// stale content.
if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker.register('/sw.js').catch(() => {
      // Non-fatal — the site works identically without a service worker, just without
      // "Add to Home Screen" installability.
    });
  });
}
