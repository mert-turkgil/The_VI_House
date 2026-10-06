/**
 * Scroll parallax on the experience hero photograph (`[data-motion="hero-parallax"]` in
 * Views/Experiences/Details.cshtml): the photograph drifts up at a third of the scroll speed while
 * the title scrolls normally, which gives the hero depth without moving any text.
 *
 * Only on devices with a fine pointer and no reduced-motion preference: on a phone a scroll-linked
 * transform costs battery and can stutter, and the hero already has its CSS entrance there. Written
 * once per frame, transform only, and only while the hero is on screen.
 */
export function initHeroParallax(): void {
  const media = document.querySelector<HTMLElement>('[data-motion="hero-parallax"]');
  if (!media) return;
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  if (!window.matchMedia('(hover: hover) and (pointer: fine)').matches) return;

  const hero = media.parentElement as HTMLElement;
  let visible = true;
  let frame = 0;

  const update = (): void => {
    frame = 0;
    if (!visible) return;
    const offset = Math.min(window.scrollY, hero.offsetHeight) * 0.3;
    media.style.transform = `translate3d(0, ${offset.toFixed(1)}px, 0)`;
  };

  new IntersectionObserver(([entry]) => {
    visible = entry.isIntersecting;
  }).observe(hero);

  window.addEventListener('scroll', () => {
    if (!frame) frame = requestAnimationFrame(update);
  }, { passive: true });

  update();
}
