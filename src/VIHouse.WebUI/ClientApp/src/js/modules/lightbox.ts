/**
 * Full-size view for a gallery: every `[data-lightbox-item]` link inside a `[data-lightbox-scope]`
 * opens that scope's `<dialog data-lightbox>` on its photograph (Views/Journal/Details.cshtml).
 *
 * The dialog is native, so showModal() provides what a hand-built overlay would have to fake: the
 * top layer, an inert page behind it, Escape to close, and focus returning to the link on close.
 * This module only swaps the photograph and adds the arrows, the arrow keys, swipe and a
 * backdrop click to close.
 *
 * The links point at the files themselves, so without script (or if showModal is missing) a click
 * simply opens the photograph in a new tab.
 */
export function initLightboxes(): void {
  document.querySelectorAll<HTMLElement>('[data-lightbox-scope]').forEach(setUp);
}

function setUp(scope: HTMLElement): void {
  const dialog = scope.querySelector<HTMLDialogElement>('dialog[data-lightbox]');
  const img = dialog?.querySelector<HTMLImageElement>('[data-lightbox-img]');
  const text = dialog?.querySelector<HTMLElement>('[data-lightbox-text]');
  const counter = dialog?.querySelector<HTMLElement>('[data-lightbox-counter]');
  const items = Array.from(scope.querySelectorAll<HTMLAnchorElement>('a[data-lightbox-item]'));
  if (!dialog || !img || !text || items.length === 0 || typeof dialog.showModal !== 'function') return;

  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  let index = 0;

  const show = (next: number): void => {
    index = (next + items.length) % items.length;
    const item = items[index];
    const caption = item.dataset.lightboxCaption ?? '';

    img.src = item.href;
    img.alt = caption;
    text.textContent = caption;
    if (counter) counter.textContent = `${String(index + 1).padStart(2, '0')} / ${String(items.length).padStart(2, '0')}`;

    if (!prefersReducedMotion) {
      // Restart the swap animation: removing the class and reading layout lets it run again.
      img.classList.remove('is-changing');
      void img.offsetWidth;
      img.classList.add('is-changing');
    }

    // The next photograph is fetched while this one is being looked at.
    const upcoming = items[(index + 1) % items.length];
    if (upcoming !== item) new Image().src = upcoming.href;
  };

  // Delegated from the scope rather than bound per link, and it runs after the carousel's own
  // handler on the track. That one cancels the click when the photograph is only partly in view
  // (it slides it in first), and a cancelled click must not also open the lightbox.
  scope.addEventListener('click', (event) => {
    const link = (event.target as HTMLElement).closest<HTMLAnchorElement>('a[data-lightbox-item]');
    if (!link || event.defaultPrevented || event.metaKey || event.ctrlKey || event.shiftKey) return;
    event.preventDefault();
    show(items.indexOf(link));
    dialog.showModal();
  });

  dialog.querySelector('[data-lightbox-close]')?.addEventListener('click', () => dialog.close());
  dialog.querySelector('[data-lightbox-prev]')?.addEventListener('click', () => show(index - 1));
  dialog.querySelector('[data-lightbox-next]')?.addEventListener('click', () => show(index + 1));

  // A click on the dark area around the photograph closes it; a click on the photograph does not.
  dialog.addEventListener('click', (event) => {
    const target = event.target as HTMLElement;
    if (target === dialog || target.classList.contains('lightbox__stage')) dialog.close();
  });

  dialog.addEventListener('keydown', (event) => {
    if (items.length < 2) return;
    if (event.key === 'ArrowLeft') { event.preventDefault(); show(index - 1); }
    if (event.key === 'ArrowRight') { event.preventDefault(); show(index + 1); }
  });

  // Horizontal swipe on touch screens. Short or mostly vertical movements are ignored, so a tap or
  // a scroll attempt does not change the photograph.
  let startX = 0;
  let startY = 0;
  dialog.addEventListener('touchstart', (event) => {
    startX = event.touches[0].clientX;
    startY = event.touches[0].clientY;
  }, { passive: true });
  dialog.addEventListener('touchend', (event) => {
    const dx = event.changedTouches[0].clientX - startX;
    const dy = event.changedTouches[0].clientY - startY;
    if (items.length > 1 && Math.abs(dx) > 50 && Math.abs(dx) > Math.abs(dy) * 1.5) show(index + (dx < 0 ? 1 : -1));
  }, { passive: true });

  // The page behind should not scroll while the photograph is open.
  const lock = (on: boolean): void => { document.documentElement.style.overflow = on ? 'hidden' : ''; };
  new MutationObserver(() => lock(dialog.open)).observe(dialog, { attributes: true, attributeFilter: ['open'] });
}
