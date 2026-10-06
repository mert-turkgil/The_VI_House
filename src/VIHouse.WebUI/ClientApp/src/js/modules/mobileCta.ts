/**
 * The sticky "Apply" bar at the bottom of an experience page on phones (`[data-mobile-cta]`).
 *
 * It slides up only once the hero's own buttons have scrolled away, and slides down again while the
 * ticket panel is on screen, so there are never two "Apply" buttons in view at once and the bar
 * never covers the panel it duplicates.
 *
 * Without this module the bar is simply always shown (the stylesheet hides it only under
 * .is-enhanced), which is the safe failure.
 */
export function initMobileCta(): void {
  const bar = document.querySelector<HTMLElement>('[data-mobile-cta]');
  const heroActions = document.querySelector<HTMLElement>('[data-hero-actions]');
  if (!bar || !heroActions || !('IntersectionObserver' in window)) return;

  const panel = document.querySelector<HTMLElement>('[data-ticket-panel]');
  let heroVisible = true;
  let panelVisible = false;

  const render = (): void => {
    const show = !heroVisible && !panelVisible;
    bar.classList.toggle('is-visible', show);
    // Hidden from assistive technology and the tab order while it is slid away.
    bar.inert = !show;
  };

  bar.classList.add('is-enhanced');

  new IntersectionObserver(([entry]) => {
    heroVisible = entry.isIntersecting;
    render();
  }).observe(heroActions);

  if (panel) {
    new IntersectionObserver(([entry]) => {
      panelVisible = entry.isIntersecting;
      render();
    }, { rootMargin: '0px 0px -20% 0px' }).observe(panel);
  }

  render();
}
