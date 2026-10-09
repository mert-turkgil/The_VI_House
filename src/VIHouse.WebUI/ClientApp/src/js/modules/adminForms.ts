/**
 * Small, panel-wide form helpers (Areas/Admin):
 *
 * - Character counters on every text field with a maxlength: "48 / 60", amber in the last 10%,
 *   red at the limit. SEO titles and summaries have soft limits that matter, and a counter is
 *   kinder than discovering the cut-off afterwards.
 * - The unsaved-changes guard: leaving a page with edits in a form marked `data-guard` (or any
 *   `.admin-form`) asks first. Submitting the form, or confirming the dialog, clears it.
 * - "How this works" help cards (_AdminHelp): open on the first visit to a section, then remember
 *   whether the person closed them.
 * - Ctrl/Cmd+S submits the form marked `data-save-shortcut` with its default save button, so a
 *   writer's reflex saves instead of opening the browser's "Save page as".
 *
 * Every text comes from data-* attributes on <body> (_AdminLayout), in the panel's language.
 */
export function initAdminForms(): void {
  initCounters();
  initUnsavedGuard();
  initHelpCards();
  initSaveShortcut();
}

function initCounters(): void {
  const template = document.body.dataset.counterTemplate || '{0} / {1}';
  const fields = document.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>(
    '.admin-shell input[maxlength]:not([type="hidden"]):not([type="number"]):not([type="file"]):not([data-no-counter]), .admin-shell textarea[maxlength]:not([data-no-counter])');

  fields.forEach((field) => {
    const max = field.maxLength;
    if (!max || max <= 0 || max > 2000) return;

    const counter = document.createElement('span');
    counter.className = 'admin-counter';
    counter.setAttribute('aria-hidden', 'true');
    field.insertAdjacentElement('afterend', counter);

    const update = (): void => {
      const length = field.value.length;
      counter.textContent = template.replace('{0}', String(length)).replace('{1}', String(max));
      counter.classList.toggle('is-near', length >= max * 0.9 && length < max);
      counter.classList.toggle('is-full', length >= max);
    };
    field.addEventListener('input', update);
    update();
  });
}

function initUnsavedGuard(): void {
  const message = document.body.dataset.unsaved || 'You have unsaved changes.';
  const forms = Array.from(document.querySelectorAll<HTMLFormElement>('form[data-guard], form.admin-form'));
  if (forms.length === 0) return;

  const dirty = new Set<HTMLFormElement>();
  let submitting = false;

  forms.forEach((form) => {
    form.addEventListener('input', () => dirty.add(form));
    form.addEventListener('change', () => dirty.add(form));
    form.addEventListener('submit', () => { submitting = true; });
  });

  // Buttons that live outside the form they submit (form="…") still submit it.
  document.addEventListener('submit', () => { submitting = true; });

  window.addEventListener('beforeunload', (event) => {
    if (submitting || dirty.size === 0) return;
    event.preventDefault();
    // Most browsers show their own wording; the message is for the ones that still use it.
    event.returnValue = message;
  });
}

function initHelpCards(): void {
  document.querySelectorAll<HTMLDetailsElement>('details[data-admin-help]').forEach((card) => {
    const key = `vih.admin.help.${card.dataset.adminHelp}`;
    let state: string | null = null;
    try { state = localStorage.getItem(key); } catch { /* private mode: always start open */ }
    if (state !== 'closed') card.open = true;
    card.addEventListener('toggle', () => {
      try { localStorage.setItem(key, card.open ? 'open' : 'closed'); } catch { /* not remembered */ }
    });
  });
}

function initSaveShortcut(): void {
  const form = document.querySelector<HTMLFormElement>('form[data-save-shortcut]');
  if (!form) return;
  document.addEventListener('keydown', (event) => {
    if (event.key.toLowerCase() !== 's' || !(event.ctrlKey || event.metaKey) || event.altKey) return;
    event.preventDefault();
    const button = document.querySelector<HTMLButtonElement>(`button[form="${form.id}"][data-default-save], form#${CSS.escape(form.id)} button[data-default-save]`);
    form.requestSubmit(button ?? undefined);
  });
}
