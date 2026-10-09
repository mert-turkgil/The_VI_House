/**
 * One confirmation dialog for the whole admin panel, replacing the browser's bare confirm() boxes.
 *
 * Put `data-confirm="What will happen, and whether it can be undone."` on a submit button, a link or
 * a <form>. A danger-styled button (`.admin-btn--danger`) or `data-confirm-danger` gets a red confirm
 * button. The dialog is a native <dialog> opened with showModal(): focus is trapped inside it, Escape
 * cancels, and focus returns to the button that opened it.
 *
 * Without this script the action simply happens, which is the same as before the bare confirm()
 * boxes it replaces, minus the question. Every one of those actions also asks the server, so nothing
 * irreversible depends on the dialog alone.
 */
let dialog: HTMLDialogElement | null = null;

function ensureDialog(): HTMLDialogElement {
  if (dialog) return dialog;
  const labels = document.body.dataset;
  dialog = document.createElement('dialog');
  dialog.className = 'admin-confirm';
  dialog.setAttribute('aria-labelledby', 'admin-confirm-title');
  dialog.setAttribute('aria-describedby', 'admin-confirm-message');
  dialog.innerHTML = `
    <form method="dialog" class="admin-confirm__box">
      <div class="admin-confirm__icon" aria-hidden="true">
        <svg viewBox="0 0 24 24"><path d="M12 3.5l9.5 16.5h-19z"/><path d="M12 10v4.5M12 17.5v.01"/></svg>
      </div>
      <h2 class="admin-confirm__title" id="admin-confirm-title"></h2>
      <p class="admin-confirm__message" id="admin-confirm-message"></p>
      <div class="admin-confirm__actions">
        <button type="submit" value="cancel" class="admin-btn admin-btn--ghost" data-confirm-cancel></button>
        <button type="submit" value="ok" class="admin-btn" data-confirm-ok></button>
      </div>
    </form>`;
  dialog.querySelector('#admin-confirm-title')!.textContent = labels.confirmTitle || 'Are you sure?';
  dialog.querySelector('[data-confirm-cancel]')!.textContent = labels.confirmCancel || 'Cancel';
  dialog.querySelector('[data-confirm-ok]')!.textContent = labels.confirmOk || 'Yes, continue';
  document.body.append(dialog);
  return dialog;
}

function ask(message: string, danger: boolean): Promise<boolean> {
  const box = ensureDialog();
  box.querySelector('#admin-confirm-message')!.textContent = message;
  box.classList.toggle('is-danger', danger);
  const ok = box.querySelector<HTMLButtonElement>('[data-confirm-ok]')!;
  ok.classList.toggle('admin-btn--danger', danger);
  box.returnValue = '';
  box.showModal();
  // The safe choice has focus: Enter on an accidental double-press cancels instead of deleting.
  box.querySelector<HTMLButtonElement>('[data-confirm-cancel]')!.focus();
  return new Promise((resolve) => {
    box.addEventListener('close', () => resolve(box.returnValue === 'ok'), { once: true });
  });
}

export function initAdminConfirm(): void {
  if (typeof HTMLDialogElement === 'undefined') return;

  // Buttons and links. Capture phase, so this runs before any other click handler on the element.
  document.addEventListener('click', async (event) => {
    const trigger = (event.target as HTMLElement).closest<HTMLElement>('button[data-confirm], a[data-confirm], input[type="submit"][data-confirm]');
    if (!trigger || trigger.dataset.confirmed === 'true') return;
    event.preventDefault();
    event.stopPropagation();

    const danger = trigger.hasAttribute('data-confirm-danger') || trigger.classList.contains('admin-btn--danger');
    if (!(await ask(trigger.dataset.confirm ?? '', danger))) return;

    if (trigger instanceof HTMLAnchorElement) {
      window.location.href = trigger.href;
      return;
    }
    const form = (trigger as HTMLButtonElement).form;
    if (!form) return;
    // requestSubmit with the button keeps its name/value (e.g. intent=unpublish, mediaId=…) and
    // runs the form's own validation and submit handlers, exactly as the original click would have.
    trigger.dataset.confirmed = 'true';
    form.requestSubmit(trigger as HTMLButtonElement);
    delete trigger.dataset.confirmed;
  }, true);

  // Whole forms marked data-confirm (submitted by Enter or any of their buttons).
  document.addEventListener('submit', async (event) => {
    const form = event.target as HTMLFormElement;
    if (!form.matches('form[data-confirm]') || form.dataset.confirmed === 'true') return;
    const submitter = (event as SubmitEvent).submitter as HTMLButtonElement | null;
    if (submitter?.dataset.confirmed === 'true') return;
    event.preventDefault();
    const danger = form.hasAttribute('data-confirm-danger') || !!submitter?.classList.contains('admin-btn--danger');
    if (!(await ask(form.dataset.confirm ?? '', danger))) return;
    form.dataset.confirmed = 'true';
    form.requestSubmit(submitter ?? undefined);
    delete form.dataset.confirmed;
  }, true);
}
