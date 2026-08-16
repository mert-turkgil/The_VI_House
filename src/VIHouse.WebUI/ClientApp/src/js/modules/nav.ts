/**
 * Mobile nav: toggles the full-screen panel, keeps the CTA reachable (brief §133), locks body
 * scroll while open, and closes on Escape or link click.
 */
export function initNav(): void {
  const burger = document.querySelector<HTMLButtonElement>('[data-nav-burger]');
  const panel = document.querySelector<HTMLElement>('[data-nav-panel]');

  if (!burger || !panel) return;

  const setOpen = (open: boolean) => {
    burger.setAttribute('aria-expanded', String(open));
    panel.dataset.open = String(open);
    document.body.classList.toggle('no-scroll', open);
  };

  burger.addEventListener('click', () => {
    const isOpen = burger.getAttribute('aria-expanded') === 'true';
    setOpen(!isOpen);
  });

  panel.querySelectorAll('a').forEach((link) => {
    link.addEventListener('click', () => setOpen(false));
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') setOpen(false);
  });

  // If the viewport is resized past the desktop breakpoint while the panel is open, close it —
  // avoids a stuck full-screen overlay behind the now-visible desktop nav.
  const desktopQuery = window.matchMedia('(min-width: 960px)');
  desktopQuery.addEventListener('change', (event) => {
    if (event.matches) setOpen(false);
  });
}
