/**
 * Scroll-triggered reveal for sections marked `data-reveal`. Each element fades and slides into
 * place once, the first time it enters the viewport, then stops being observed — re-animating on
 * every scroll past would read as a glitch rather than a flourish.
 *
 * The `.reveal` base class (which hides the element) is added here in JS rather than in the markup,
 * so a visitor with JS disabled or a failed bundle sees the page fully rendered instead of a blank
 * one — the animation is an enhancement, never a precondition for reading the site.
 */
export function initScrollReveal(): void {
  const targets = Array.from(document.querySelectorAll<HTMLElement>('[data-reveal]'));
  if (targets.length === 0) return;

  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (prefersReducedMotion || !('IntersectionObserver' in window)) return;

  targets.forEach((el) => el.classList.add('reveal'));

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;

        const el = entry.target as HTMLElement;
        // Stagger children of a revealed section (cards in a row) so they arrive in sequence
        // rather than all at once — set as a custom property the stylesheet reads.
        const delay = Number(el.dataset.revealDelay ?? '0');
        el.style.setProperty('--reveal-delay', `${delay}ms`);
        el.classList.add('is-visible');
        observer.unobserve(el);
      });
    },
    // Fire slightly before the element is fully in view, so the motion finishes around the point
    // the reader's eye actually reaches it.
    { threshold: 0.12, rootMargin: '0px 0px -60px 0px' },
  );

  targets.forEach((el) => observer.observe(el));
}
