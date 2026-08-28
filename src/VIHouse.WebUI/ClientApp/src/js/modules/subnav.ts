/**
 * Highlights the current section in the Experiences detail sub-nav as the reader scrolls.
 *
 * IntersectionObserver rather than GSAP ScrollTrigger: this is a boolean per section, not a
 * scroll-linked animation, and it would be a poor trade to pull ~38 KB of animation library into
 * every page's bundle to toggle a class.
 *
 * The sub-nav itself is `position: sticky` with plain in-page anchors, so it works completely
 * without this — the links jump to the right place regardless. All this adds is knowing where you
 * are, which is why it can fail silently.
 */
export function initSubnav(): void {
  const nav = document.querySelector<HTMLElement>('[data-subnav]');
  if (!nav) return;

  const links = new Map<string, HTMLAnchorElement>();
  nav.querySelectorAll<HTMLAnchorElement>('[data-subnav-link]').forEach((link) => {
    const id = link.dataset.subnavLink;
    if (id) links.set(id, link);
  });

  const sections = Array.from(document.querySelectorAll<HTMLElement>('[data-subnav-section]'));
  if (sections.length === 0 || links.size === 0) return;

  if (!('IntersectionObserver' in window)) return;

  const ACTIVE = 'experience-subnav__link--active';

  function setActive(id: string): void {
    links.forEach((link, key) => {
      const isActive = key === id;
      link.classList.toggle(ACTIVE, isActive);
      // aria-current is the part that matters for a screen reader — the class is only paint.
      if (isActive) link.setAttribute('aria-current', 'true');
      else link.removeAttribute('aria-current');
    });

    // Keep the active chip in view on narrow screens, where the sub-nav scrolls horizontally.
    // 'nearest' so it only moves when it actually has to.
    links.get(id)?.scrollIntoView({ block: 'nearest', inline: 'nearest' });
  }

  // Track every section's visibility rather than reacting to individual entries: with several
  // short sections on screen at once, "the last one that fired" is not the same as "the one the
  // reader is looking at". Topmost visible section wins.
  const visible = new Set<string>();

  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        const id = (entry.target as HTMLElement).dataset.subnavSection;
        if (!id) continue;
        if (entry.isIntersecting) visible.add(id);
        else visible.delete(id);
      }

      const topmost = sections.find((section) => visible.has(section.dataset.subnavSection ?? ''));
      if (topmost?.dataset.subnavSection) setActive(topmost.dataset.subnavSection);
    },
    {
      // The negative top margin is the sticky header plus the sub-nav itself, so a section counts as
      // current only once it clears the chrome covering it.
      rootMargin: '-140px 0px -55% 0px',
      threshold: 0,
    },
  );

  sections.forEach((section) => observer.observe(section));
}
