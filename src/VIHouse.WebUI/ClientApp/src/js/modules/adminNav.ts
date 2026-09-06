/**
 * The admin sidebar as a drawer on a small screen.
 *
 * The panel has had no responsive story at all: a fixed 248px sidebar and tables with no overflow
 * wrapper, so below about 900px the navigation eats a third of the viewport and seven-column tables
 * clip off the edge. Approving an application from a phone was not possible.
 *
 * The toggle button is created here rather than in the layout, so it can never appear as a dead
 * control if the bundle fails to load — the same reasoning recoveryCodes.ts uses for its download
 * button.
 */
export function initAdminNav(): void {
  const shell = document.querySelector<HTMLElement>('.admin-shell');
  const sidebar = document.querySelector<HTMLElement>('.admin-sidebar');
  if (!shell || !sidebar) return;

  const toggle = document.createElement('button');
  toggle.type = 'button';
  toggle.className = 'admin-burger';
  toggle.setAttribute('aria-label', 'Menu');
  toggle.setAttribute('aria-expanded', 'false');
  toggle.setAttribute('aria-controls', 'admin-sidebar');
  toggle.innerHTML = '<span></span><span></span><span></span>';

  sidebar.id = 'admin-sidebar';

  const scrim = document.createElement('div');
  scrim.className = 'admin-scrim';
  scrim.hidden = true;

  function setOpen(open: boolean): void {
    shell!.classList.toggle('admin-shell--nav-open', open);
    toggle.setAttribute('aria-expanded', String(open));
    scrim.hidden = !open;
    // Only locked while the drawer covers the page; releasing it on close matters because the
    // drawer can also be closed by a resize.
    document.body.style.overflow = open ? 'hidden' : '';
  }

  toggle.addEventListener('click', () => setOpen(!shell.classList.contains('admin-shell--nav-open')));
  scrim.addEventListener('click', () => setOpen(false));

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') setOpen(false);
  });

  // Following a link inside the drawer should close it, or the destination renders underneath.
  sidebar.addEventListener('click', (event) => {
    if ((event.target as HTMLElement | null)?.closest('a')) setOpen(false);
  });

  // Crossing back to the desktop layout leaves the drawer state stale otherwise: the sidebar
  // becomes permanently visible again while the scroll lock is still on.
  window.matchMedia('(min-width: 960px)').addEventListener('change', (event) => {
    if (event.matches) setOpen(false);
  });

  document.body.prepend(scrim);
  document.body.prepend(toggle);
}
