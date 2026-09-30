/**
 * The journal media library's ordering (Areas/Admin/Views/AdminJournal/Edit.cshtml,
 * `form[data-media-library]`).
 *
 * The order is simply the order of the cards in the form: every card posts `items.Index` with its
 * media id, and model binding keeps the posted order. So reordering is DOM-only here, and nothing
 * is renumbered. Two ways to move a card:
 *   - drag it by its grip (native drag and drop, so no library is needed), or
 *   - its up/down buttons, which also cover the keyboard and touch screens, where native drag does
 *     not work.
 *
 * Also kept current here: the position badge on each thumbnail (its place in the public gallery,
 * counting only photographs that are shown there), the faded look of a photograph switched out of
 * the gallery, and the "unsaved changes" marker, with a prompt before leaving the page unsaved.
 */
export function initMediaLibrary(): void {
  const form = document.querySelector<HTMLFormElement>('form[data-media-library]');
  const list = form?.querySelector<HTMLElement>('[data-media-list]');
  if (!form || !list) return;

  const dirtyMark = form.querySelector<HTMLElement>('[data-media-dirty]');
  const announcer = form.querySelector<HTMLElement>('[data-media-announce]');
  const cards = (): HTMLElement[] => Array.from(list.querySelectorAll<HTMLElement>(':scope > [data-media-item]'));

  let dirty = false;
  let submitting = false;

  const markDirty = (): void => {
    dirty = true;
    if (dirtyMark) dirtyMark.hidden = false;
  };

  const refresh = (): void => {
    let position = 0;
    const all = cards();
    all.forEach((card, i) => {
      const toggle = card.querySelector<HTMLInputElement>('input[type="checkbox"][name$=".ShowInGallery"]');
      const inGallery = !!toggle?.checked;
      card.classList.toggle('is-hidden-from-gallery', !!toggle && !inGallery);

      const badge = card.querySelector<HTMLElement>('[data-media-position]');
      if (badge) badge.textContent = inGallery ? String(++position) : '';

      const up = card.querySelector<HTMLButtonElement>('[data-media-move="-1"]');
      const down = card.querySelector<HTMLButtonElement>('[data-media-move="1"]');
      if (up) up.disabled = i === 0;
      if (down) down.disabled = i === all.length - 1;
    });
  };

  const flash = (card: HTMLElement): void => {
    card.classList.remove('is-moved');
    void card.offsetWidth;
    card.classList.add('is-moved');
  };

  const announce = (card: HTMLElement): void => {
    if (!announcer) return;
    const template = announcer.dataset.template ?? '{0} / {1}';
    const index = cards().indexOf(card) + 1;
    announcer.textContent = template.replace('{0}', String(index)).replace('{1}', String(cards().length));
  };

  // --- Arrow buttons ---------------------------------------------------------------------------------

  list.addEventListener('click', (event) => {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>('[data-media-move]');
    if (!button) return;
    const card = button.closest<HTMLElement>('[data-media-item]');
    if (!card) return;

    if (button.dataset.mediaMove === '-1' && card.previousElementSibling) {
      list.insertBefore(card, card.previousElementSibling);
    } else if (button.dataset.mediaMove === '1' && card.nextElementSibling) {
      list.insertBefore(card.nextElementSibling, card);
    } else {
      return;
    }

    refresh();
    markDirty();
    flash(card);
    announce(card);
    // Keep focus on the same button, which has moved with its card, so repeated presses keep going.
    if (!button.disabled) button.focus();
    else card.querySelector<HTMLButtonElement>('[data-media-move]:not(:disabled)')?.focus();
  });

  // --- Drag and drop ---------------------------------------------------------------------------------
  // Only a drag that starts on the grip moves a card. The cards are draggable="true" (the attribute
  // has to be on the element that moves), so a drag from anywhere else is cancelled. Otherwise
  // selecting text in a caption field would pick the whole card up.

  let dragged: HTMLElement | null = null;
  let fromGrip = false;

  list.addEventListener('pointerdown', (event) => {
    fromGrip = !!(event.target as HTMLElement).closest('[data-media-grip]');
  });

  list.addEventListener('dragstart', (event) => {
    const card = (event.target as HTMLElement).closest<HTMLElement>('[data-media-item]');
    if (!card || !fromGrip) {
      event.preventDefault();
      return;
    }
    dragged = card;
    card.classList.add('is-dragging');
    if (event.dataTransfer) {
      event.dataTransfer.effectAllowed = 'move';
      // Firefox will not start a drag without some data.
      event.dataTransfer.setData('text/plain', card.id);
    }
  });

  list.addEventListener('dragover', (event) => {
    if (!dragged) return;
    const over = (event.target as HTMLElement).closest<HTMLElement>('[data-media-item]');
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
    if (!over || over === dragged) return;

    // Upper half of the card: go before it. Lower half: after it. The list reorders live, so the
    // gap follows the pointer and the drop itself has nothing left to do.
    const box = over.getBoundingClientRect();
    const after = event.clientY > box.top + box.height / 2;
    const reference = after ? over.nextElementSibling : over;
    if (reference !== dragged && dragged.nextElementSibling !== reference) {
      list.insertBefore(dragged, reference);
      refresh();
    }
    cards().forEach((c) => c.classList.toggle('is-drop-target', c === over));
  });

  list.addEventListener('drop', (event) => event.preventDefault());

  list.addEventListener('dragend', () => {
    if (!dragged) return;
    const card = dragged;
    dragged = null;
    card.classList.remove('is-dragging');
    cards().forEach((c) => c.classList.remove('is-drop-target'));
    refresh();
    markDirty();
    flash(card);
    announce(card);
  });

  // --- Captions, switches, leaving -------------------------------------------------------------------

  form.addEventListener('input', markDirty);
  form.addEventListener('change', () => { refresh(); markDirty(); });

  // Every button in the form posts it (Save, Use as cover, Delete), and none of them should trigger
  // the leave warning.
  form.addEventListener('submit', () => { submitting = true; });

  window.addEventListener('beforeunload', (event) => {
    if (dirty && !submitting) event.preventDefault();
  });

  refresh();
}
