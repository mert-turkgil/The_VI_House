/**
 * The admin sidebar as a drawer on a small screen, plus desktop collapse and quick nav search.
 */
export function initAdminNav(): void {
  const shell = document.querySelector<HTMLElement>('.admin-shell');
  const sidebar = document.querySelector<HTMLElement>('.admin-sidebar');
  if (!shell || !sidebar) return;

  const desktopQuery = window.matchMedia('(min-width: 960px)');
  const storageKey = 'vih-admin-sidebar-collapsed';
  const collapseToggle = document.querySelector<HTMLButtonElement>('[data-admin-sidebar-collapse]');
  const collapseIcon = collapseToggle?.querySelector<HTMLElement>('[data-admin-sidebar-collapse-icon]');
  const searchInputs = Array.from(document.querySelectorAll<HTMLInputElement>('[data-admin-nav-search]'));
  const navLinks = Array.from(sidebar.querySelectorAll<HTMLAnchorElement>('[data-nav-label]'));
  const emptyState = sidebar.querySelector<HTMLElement>('[data-admin-nav-empty]');
  const topbar = document.querySelector<HTMLElement>('.admin-topbar');

  let toggle = document.querySelector<HTMLButtonElement>('.admin-burger');
  if (!toggle) {
    toggle = document.createElement('button');
    toggle.type = 'button';
    toggle.className = 'admin-burger';
    toggle.setAttribute('aria-label', 'Menu');
    toggle.setAttribute('aria-expanded', 'false');
    toggle.setAttribute('aria-controls', 'admin-sidebar');
    toggle.innerHTML = '<span></span><span></span><span></span>';
    topbar?.prepend(toggle);
  }

  sidebar.id = 'admin-sidebar';

  let scrim = document.querySelector<HTMLElement>('.admin-scrim');
  if (!scrim) {
    scrim = document.createElement('div');
    scrim.className = 'admin-scrim';
    scrim.hidden = true;
    document.body.prepend(scrim);
  }

  function collapseIconMarkup(collapsed: boolean): string {
    return collapsed
      ? '<svg class="icon icon--panel-left-open" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><rect x="3" y="4" width="18" height="16" rx="2"></rect><path d="M9 4v16"></path><path d="m12 9 3 3-3 3"></path></svg>'
      : '<svg class="icon icon--panel-left-close" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false"><rect x="3" y="4" width="18" height="16" rx="2"></rect><path d="M9 4v16"></path><path d="m15 9-3 3 3 3"></path></svg>';
  }

  function setOpen(open: boolean): void {
    shell.classList.toggle('admin-shell--nav-open', open);
    toggle!.setAttribute('aria-expanded', String(open));
    scrim!.hidden = !open;
    document.body.style.overflow = open ? 'hidden' : '';
  }

  function setCollapsed(collapsed: boolean): void {
    const effectiveCollapsed = desktopQuery.matches ? collapsed : false;

    shell.classList.toggle('admin-shell--nav-collapsed', effectiveCollapsed);
    collapseToggle?.setAttribute('aria-pressed', String(effectiveCollapsed));
    collapseToggle?.setAttribute('aria-label', effectiveCollapsed ? 'Expand sidebar' : 'Collapse sidebar');
    collapseToggle?.setAttribute('title', effectiveCollapsed ? 'Expand sidebar' : 'Collapse sidebar');
    if (collapseIcon) collapseIcon.innerHTML = collapseIconMarkup(effectiveCollapsed);

    try {
      window.localStorage.setItem(storageKey, collapsed ? 'true' : 'false');
    } catch {
      // Ignore storage failures and keep the current in-memory state.
    }
  }

  function applyFilter(rawQuery: string): void {
    const query = rawQuery.trim().toLowerCase();
    let visible = 0;

    navLinks.forEach((link) => {
      const label = (link.dataset.navLabel ?? '').toLowerCase();
      const matches = query.length === 0 || label.includes(query);
      const entry = link.closest<HTMLElement>('[data-nav-entry]') ?? link;
      entry.hidden = !matches;
      if (matches) visible += 1;
    });

    if (emptyState) emptyState.hidden = visible !== 0;
  }

  toggle.addEventListener('click', () => setOpen(!shell.classList.contains('admin-shell--nav-open')));
  scrim.addEventListener('click', () => setOpen(false));
  collapseToggle?.addEventListener('click', () => {
    setCollapsed(!shell.classList.contains('admin-shell--nav-collapsed'));
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') setOpen(false);
  });

  sidebar.addEventListener('click', (event) => {
    if (!desktopQuery.matches && (event.target as HTMLElement | null)?.closest('a')) setOpen(false);
  });

  desktopQuery.addEventListener('change', (event) => {
    if (event.matches) {
      setOpen(false);

      try {
        setCollapsed(window.localStorage.getItem(storageKey) === 'true');
      } catch {
        setCollapsed(false);
      }
    } else {
      shell.classList.remove('admin-shell--nav-collapsed');
    }
  });

  searchInputs.forEach((input) => {
    input.addEventListener('input', () => {
      const { value } = input;

      searchInputs.forEach((peer) => {
        if (peer !== input && peer.value !== value) peer.value = value;
      });

      applyFilter(value);
    });
  });

  try {
    setCollapsed(window.localStorage.getItem(storageKey) === 'true');
  } catch {
    setCollapsed(false);
  }

  if (collapseIcon) collapseIcon.innerHTML = collapseIconMarkup(shell.classList.contains('admin-shell--nav-collapsed'));
  applyFilter(searchInputs[0]?.value ?? '');
}
