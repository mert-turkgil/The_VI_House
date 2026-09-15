/**
 * The account sidebar on phones: `[data-account-nav-toggle]` folds and unfolds `[data-account-nav]`.
 * Above 720px the stylesheet shows the nav and hides the toggle, so this never runs visibly there.
 * Closes on Escape and on a click outside, the way the header's own menus do.
 */
export function initAccountNav(): void {
  const root = document.querySelector<HTMLElement>('[data-account-nav-root]');
  const toggle = root?.querySelector<HTMLButtonElement>('[data-account-nav-toggle]');
  const nav = root?.querySelector<HTMLElement>('[data-account-nav]');
  if (!root || !toggle || !nav) return;

  const setOpen = (open: boolean) => {
    nav.classList.toggle('is-open', open);
    toggle.setAttribute('aria-expanded', String(open));
  };

  toggle.addEventListener('click', () => setOpen(!nav.classList.contains('is-open')));

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && nav.classList.contains('is-open')) {
      setOpen(false);
      toggle.focus();
    }
  });

  document.addEventListener('click', (event) => {
    if (!nav.classList.contains('is-open')) return;
    if (root.contains(event.target as Node)) return;
    setOpen(false);
  });
}
