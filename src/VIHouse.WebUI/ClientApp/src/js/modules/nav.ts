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

/**
 * Shared behavior for every <details>-as-dropdown in the header (language switcher, notification
 * bell, ...): opening/closing itself needs no JS at all, this only adds the polish a native
 * <details> doesn't give for free — closing on an outside click, on Escape, and immediately on
 * picking a link inside it (so it doesn't still look open while the next page is loading).
 */
export function initDismissableDropdown(selector: string): void {
  const dropdowns = document.querySelectorAll<HTMLDetailsElement>(selector);
  if (dropdowns.length === 0) return;

  document.addEventListener('click', (event) => {
    dropdowns.forEach((el) => {
      if (el.open && !el.contains(event.target as Node)) el.open = false;
    });
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') dropdowns.forEach((el) => { el.open = false; });
  });

  dropdowns.forEach((el) => {
    el.querySelectorAll('a').forEach((link) => {
      link.addEventListener('click', () => { el.open = false; });
    });
  });
}
