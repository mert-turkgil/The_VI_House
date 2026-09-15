import '../scss/main.scss';
import { initNav, initDismissableDropdown, initSiteSearch } from './modules/nav';
import { initScrollReveal } from './modules/reveal';
import { initStatCounters } from './modules/counter';
import { initCarousels, initHeroSlider } from './modules/carousel';
import { initRecoveryCodeDownload } from './modules/recoveryCodes';
import { initExperienceFilters } from './modules/filters';
import { initSubnav } from './modules/subnav';
import { initVideoEmbeds } from './modules/video';
import { initExperienceGate } from './modules/gate';
import { initAuthBackdrop } from './modules/authBackdrop';
import { initPasswordToggle } from './modules/passwordToggle';
import { initViewTransitions } from './modules/transitions';
import { initJoinPage } from './modules/join';

// Each module is started on its own so that one throwing — a page shape it did not expect, a
// browser quirk — cannot take the rest of the page's behaviour down with it. Before this, one
// failure early in the list meant no carousel, no nav panel and no password toggle on that page.
function run(name: string, init: () => void): void {
  try {
    init();
  } catch (error) {
    console.error(`[vih] ${name} failed to initialise`, error);
  }
}

document.addEventListener('DOMContentLoaded', () => {
  run('nav', initNav);
  run('lang-switch', () => initDismissableDropdown('.lang-switch'));
  run('notif-bell', () => initDismissableDropdown('.notif-bell'));
  run('site-search-dropdown', () => initDismissableDropdown('.site-search'));
  run('site-search', initSiteSearch);
  run('reveal', initScrollReveal);
  run('counters', initStatCounters);
  run('carousels', initCarousels);
  run('hero', initHeroSlider);
  run('recovery-codes', initRecoveryCodeDownload);
  run('filters', initExperienceFilters);
  run('subnav', initSubnav);
  run('video', initVideoEmbeds);
  run('gate', initExperienceGate);
  run('auth-backdrop', initAuthBackdrop);
  run('password-toggle', initPasswordToggle);
  run('view-transitions', initViewTransitions);
  run('join', initJoinPage);
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
