/**
 * Cross-document view transitions between a card and the page it opens.
 *
 * The stylesheet opts every same-origin navigation into a short root cross-fade
 * (`@view-transition { navigation: auto }` in _base.scss). This module adds the part CSS cannot:
 * naming the ONE cover the visitor just clicked so the browser morphs it into the detail page's
 * hero, which carries the same `view-transition-name: experience-cover` from the server.
 *
 * The name has to be applied at click time rather than in the markup because a name may appear
 * once per page at most — a second element with it makes the browser skip the transition
 * entirely, and the homepage routinely lists the same experience in two strips.
 *
 * Browsers without the API simply navigate; that is the whole fallback.
 */
const COVER_NAME = 'experience-cover';

export function initViewTransitions(): void {
  if (!('startViewTransition' in document)) return;
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

  const clearNames = () => {
    document.querySelectorAll<HTMLElement>('[data-vt-cover] .cover').forEach((el) => {
      el.style.viewTransitionName = '';
    });
  };

  document.addEventListener('click', (event) => {
    // Modified clicks open a new tab or window; there is no transition to draw for those.
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    const link = (event.target as HTMLElement).closest<HTMLAnchorElement>('a[data-vt-cover]');
    if (!link || link.target === '_blank') return;
    const cover = link.querySelector<HTMLElement>('.cover');
    if (!cover) return;
    clearNames();
    cover.style.viewTransitionName = COVER_NAME;
  });

  // Coming back through the bfcache restores the page with the clicked cover still named — which
  // is what lets the hero morph back into its card. Once that has played, the name has to go, or
  // the next click on a different card would make two.
  window.addEventListener('pageshow', () => {
    window.setTimeout(clearNames, 700);
  });
}
