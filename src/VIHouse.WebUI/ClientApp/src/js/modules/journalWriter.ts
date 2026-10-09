import { editorFor } from './editor';

/**
 * The journal writer (Areas/Admin/Views/AdminJournal/Edit.cshtml, `[data-writer]`).
 *
 * - Word count and reading time under the editor, live (CKEditor's WordCount plugin, with a plain
 *   count of the textarea as the fallback).
 * - Autosave to this browser every couple of seconds while typing, per post and language. When the
 *   page opens with a local copy newer than the server's, a bar offers to restore it, so a closed
 *   tab, a crash or an expired session never costs an article. Cleared when the form is submitted.
 * - Focus mode: the editor fills the screen and everything else steps aside. Esc leaves it.
 * - The address and a search-result preview, updated as the slug, title and search fields change.
 * - On narrower screens the sidebar cards start folded, so the article comes first.
 *
 * Every label is a data-* attribute on the root, in the panel's language.
 */
const AUTOSAVE_DELAY_MS = 1500;
const WORDS_PER_MINUTE = 220;

interface Draft {
  title: string;
  excerpt: string;
  body: string;
  at: number;
}

export function initJournalWriter(): void {
  const root = document.querySelector<HTMLElement>('[data-writer]');
  const form = document.querySelector<HTMLFormElement>('#journal-writer');
  if (!root || !form) return;

  const labels = root.dataset;
  const title = form.querySelector<HTMLInputElement>('[data-writer-title]');
  const excerpt = form.querySelector<HTMLTextAreaElement>('[data-writer-excerpt]');
  const body = form.querySelector<HTMLTextAreaElement>('[data-writer-body]');
  const count = root.querySelector<HTMLElement>('[data-writer-count]');
  const autosaveNote = root.querySelector<HTMLElement>('[data-writer-autosave]');
  const key = `vih.writer.${labels.postId}.${labels.culture}`;
  const serverUpdatedAt = Number(labels.updatedAt ?? '0');

  // --- Word count -------------------------------------------------------------------------------------
  const showCount = (words: number): void => {
    if (!count) return;
    const minutes = Math.max(1, Math.round(words / WORDS_PER_MINUTE));
    count.textContent = `${(labels.wordsLabel ?? '{0} words').replace('{0}', words.toLocaleString())} · ${(labels.minutesLabel ?? '{0} min read').replace('{0}', String(minutes))}`;
  };
  const countText = (html: string): number =>
    (html.replace(/<[^>]+>/g, ' ').match(/[\p{L}\p{N}]+/gu) ?? []).length;

  root.addEventListener('editor:wordcount', (event) => showCount((event as CustomEvent<{ words: number }>).detail.words));
  if (body) showCount(countText(body.value));

  // --- Autosave to this browser -------------------------------------------------------------------------
  const current = (): Draft => ({
    title: title?.value ?? '',
    excerpt: excerpt?.value ?? '',
    body: body ? (editorFor(body)?.getData() ?? body.value) : '',
    at: Date.now(),
  });

  const time = (at: number): string => new Date(at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

  let timer = 0;
  form.addEventListener('input', () => {
    window.clearTimeout(timer);
    timer = window.setTimeout(() => {
      const draft = current();
      try {
        localStorage.setItem(key, JSON.stringify(draft));
        if (autosaveNote) autosaveNote.textContent = (labels.autosavedLabel ?? 'Saved on this device at {0}').replace('{0}', time(draft.at));
      } catch {
        // Storage full or disabled: the server save still works, there is just no safety net.
      }
    }, AUTOSAVE_DELAY_MS);
  });

  form.addEventListener('submit', () => {
    window.clearTimeout(timer);
    try { localStorage.removeItem(key); } catch { /* nothing to clear */ }
  });

  let stored: Draft | null = null;
  try { stored = JSON.parse(localStorage.getItem(key) ?? 'null') as Draft | null; } catch { stored = null; }
  if (stored && stored.at > serverUpdatedAt) {
    const restoreWhenReady = (): void => {
      const now = current();
      if (now.title === stored!.title && now.excerpt === stored!.excerpt && now.body === stored!.body) {
        try { localStorage.removeItem(key); } catch { /* nothing to clear */ }
        return;
      }
      offerRestore(form, stored!, labels, time(stored!.at), (draft) => {
        if (title) title.value = draft.title;
        if (excerpt) excerpt.value = draft.excerpt;
        const editor = body ? editorFor(body) : null;
        if (editor) editor.setData(draft.body);
        else if (body) body.value = draft.body;
        form.dispatchEvent(new Event('input', { bubbles: true }));
      }, () => {
        try { localStorage.removeItem(key); } catch { /* nothing to clear */ }
      });
    };
    // Compare against the editor's own output once it exists, or the restore bar would appear for a
    // copy that only differs in how CKEditor normalises the HTML.
    if (body && !editorFor(body) && window.CKEDITOR) {
      body.addEventListener('editor:ready', restoreWhenReady, { once: true });
    } else {
      restoreWhenReady();
    }
  }

  // --- Focus mode ----------------------------------------------------------------------------------------
  const focusButton = root.querySelector<HTMLButtonElement>('[data-writer-focus]');
  const setFocus = (on: boolean): void => {
    root.classList.toggle('writer--focus', on);
    document.body.classList.toggle('writer-focus-open', on);
    if (focusButton) {
      focusButton.setAttribute('aria-pressed', String(on));
      focusButton.textContent = (on ? focusButton.dataset.labelOff : focusButton.dataset.labelOn) ?? focusButton.textContent;
    }
    if (on && body) editorFor(body)?.editing.view.focus();
  };
  focusButton?.addEventListener('click', () => setFocus(!root.classList.contains('writer--focus')));
  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && root.classList.contains('writer--focus')) setFocus(false);
  });

  // --- Address and search preview ----------------------------------------------------------------------------
  const base = labels.urlBase ?? '/journal/';
  const slug = document.querySelector<HTMLInputElement>('[data-writer-slug]');
  const seoTitle = document.querySelector<HTMLInputElement>('[data-writer-seo-title]');
  const seoDesc = document.querySelector<HTMLTextAreaElement>('[data-writer-seo-desc]');
  const url = document.querySelector<HTMLElement>('[data-writer-url]');
  const serpUrl = document.querySelector<HTMLElement>('[data-serp-url]');
  const serpTitle = document.querySelector<HTMLElement>('[data-serp-title]');
  const serpDesc = document.querySelector<HTMLElement>('[data-serp-desc]');

  const previewSlug = (value: string): string => value.trim().toLowerCase()
    .replace(/ı/g, 'i').replace(/ş/g, 's').replace(/ğ/g, 'g').replace(/ü/g, 'u').replace(/ö/g, 'o').replace(/ç/g, 'c')
    .replace(/ä/g, 'ae').replace(/ß/g, 'ss').replace(/õ/g, 'o')
    .replace(/[^a-z0-9]+/g, '-').replace(/^-+|-+$/g, '');

  const clip = (text: string, max: number): string => (text.length > max ? `${text.slice(0, max - 1).trimEnd()}…` : text);

  const updatePreview = (): void => {
    const address = base + previewSlug(slug?.value ?? '');
    if (url) url.textContent = address;
    if (serpUrl) serpUrl.textContent = address;
    if (serpTitle) serpTitle.textContent = clip((seoTitle?.value || title?.value || '').trim(), 60);
    if (serpDesc) {
      const fromBody = body ? body.value.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim() : '';
      serpDesc.textContent = clip((seoDesc?.value || excerpt?.value || fromBody).trim(), 158);
    }
  };
  [slug, seoTitle, seoDesc, title, excerpt, body].forEach((field) => field?.addEventListener('input', updatePreview));
  updatePreview();

  // --- Sidebar cards fold on narrower screens -------------------------------------------------------------------
  if (window.matchMedia('(max-width: 1099px)').matches) {
    root.querySelectorAll<HTMLDetailsElement>('details.writer-card').forEach((card) => { card.open = false; });
  }
}

function offerRestore(
  form: HTMLFormElement, draft: Draft, labels: DOMStringMap, at: string,
  onRestore: (draft: Draft) => void, onDiscard: () => void,
): void {
  const bar = document.createElement('div');
  bar.className = 'writer-restore';
  bar.setAttribute('role', 'alert');
  const text = document.createElement('span');
  text.textContent = (labels.restoreLabel ?? 'Unsaved text from {0} was found on this device.').replace('{0}', at);
  const yes = document.createElement('button');
  yes.type = 'button';
  yes.className = 'admin-btn admin-btn--sm';
  yes.textContent = labels.restoreYes ?? 'Restore it';
  const no = document.createElement('button');
  no.type = 'button';
  no.className = 'admin-btn admin-btn--ghost admin-btn--sm';
  no.textContent = labels.restoreNo ?? 'Discard';
  bar.append(text, yes, no);
  form.prepend(bar);

  yes.addEventListener('click', () => { onRestore(draft); bar.remove(); });
  no.addEventListener('click', () => { onDiscard(); bar.remove(); });
}
