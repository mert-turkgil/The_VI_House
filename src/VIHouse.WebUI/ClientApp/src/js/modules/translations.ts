/**
 * Click-to-edit for the translations table.
 *
 * 721 keys across four languages is 2,884 editable values. Rendering them all as <textarea> would
 * produce a page nothing could scroll, and paging on the server would mean the search box only ever
 * searched the fifty rows in front of you. So every cell renders as text and becomes an editor only
 * when it is clicked — the DOM stays light enough to hold the whole set, and the search runs across
 * all of it.
 *
 * Saving is one cell at a time. Each POST carries the value the page was rendered with, and the
 * server refuses the write if the file has changed since — otherwise the second of two people
 * editing the same string silently erases the first.
 */
import { toast } from './toasts';

export function initTranslationEditor(): void {
  const table = document.querySelector<HTMLTableElement>('[data-translations]');
  if (!table) return;

  const saveUrl = table.dataset.saveUrl;
  if (!saveUrl) return;

  const token = document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]')?.value ?? '';

  let open: HTMLTableCellElement | null = null;

  /**
   * The value as it currently stands, read from the cell's own text rather than a data- attribute.
   *
   * This matters more than it looks: HTML collapses newlines inside an attribute value, so a
   * data-original round trip silently returned "Ana Sayfa" for a stored "Ana Sayfa
". The baseline
   * then never matched the file, the server rejected every save as a conflict, and the cell could
   * not be edited again. It would have made all four multi-paragraph Legal bodies permanently
   * uneditable too. A text node keeps its newlines.
   */
  function baseline(cell: HTMLTableCellElement): string {
    return cell.textContent ?? '';
  }

  function beginEdit(cell: HTMLTableCellElement): void {
    if (open === cell || cell.querySelector('textarea')) return;
    if (open) closeEdit(open, baseline(open));

    const current = baseline(cell);
    cell.classList.add('is-editing');
    cell.removeAttribute('role');
    cell.removeAttribute('tabindex');
    cell.textContent = '';

    const editor = document.createElement('textarea');
    editor.className = 'admin-cell-edit__input';
    editor.value = current;
    editor.rows = Math.min(8, current.split('\n').length + 1);
    cell.append(editor);
    editor.focus();
    editor.setSelectionRange(editor.value.length, editor.value.length);

    open = cell;

    editor.addEventListener('keydown', (event) => {
      if (event.key === 'Escape') {
        event.preventDefault();
        closeEdit(cell, current);
      }
      // Enter saves. Shift+Enter is the new line — four of these values are multi-paragraph legal
      // bodies, so a way to type one has to exist, but the common case by far is a single line and
      // reaching for a modifier to save every short string gets old immediately.
      // Ctrl/Cmd+Enter is kept as an alias because it is the reflex in most editors.
      if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        editor.blur();
      }
    });

    editor.addEventListener('blur', () => void commit(cell, editor.value, current));
  }

  function editorIn(cell: HTMLTableCellElement): HTMLTextAreaElement | null {
    return cell.querySelector<HTMLTextAreaElement>('.admin-cell-edit__input');
  }

  function closeEdit(cell: HTMLTableCellElement, value: string): void {
    cell.classList.remove('is-editing');
    cell.textContent = value;
    cell.setAttribute('role', 'button');
    cell.setAttribute('tabindex', '0');
    if (open === cell) open = null;
  }

  async function commit(cell: HTMLTableCellElement, value: string, previous: string): Promise<void> {
    if (value === previous) {
      closeEdit(cell, previous);
      return;
    }

    cell.classList.add('is-saving');

    try {
      const body = new FormData();
      body.set('key', cell.dataset.key ?? '');
      body.set('culture', cell.dataset.culture ?? '');
      body.set('value', value);
      body.set('original', previous);
      body.set('__RequestVerificationToken', token);

      const response = await fetch(saveUrl!, { method: 'POST', body });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      const result = (await response.json()) as { ok: boolean; error?: string; current?: string };

      if (!result.ok) {
        toast(result.error ?? 'That could not be saved.', 'error');

        if (typeof result.current === 'string') {
          // A conflict: the file moved under us. Adopt what is actually on disk as the new
          // baseline, or every subsequent attempt is refused for the same reason and the cell can
          // never be edited again.
          closeEdit(cell, result.current);
        } else {
          // A validation problem — a broken placeholder, an emptied English source. Keep the typed
          // text in an open editor so it can be corrected rather than retyped from scratch.
          cell.classList.add('is-invalid');
          editorIn(cell)?.focus();
        }
        return;
      }

      cell.classList.remove('is-invalid');

      closeEdit(cell, value);
      cell.classList.add('is-saved');
      window.setTimeout(() => cell.classList.remove('is-saved'), 1200);

      // The row is no longer identical to the English, so drop the flag it was carrying.
      cell.closest('tr')?.classList.remove('is-untranslated');
    } catch {
      // The text is still in the editor and the connection may come back; discarding what they
      // typed would be the worst possible response to a dropped request.
      toast('That could not be saved — the connection failed.', 'error');
      cell.classList.add('is-invalid');
    } finally {
      cell.classList.remove('is-saving');
    }
  }

  // Delegated, so cells keep working after a search re-renders the rows.
  table.addEventListener('click', (event) => {
    const cell = (event.target as HTMLElement | null)?.closest<HTMLTableCellElement>('.admin-cell-edit');
    if (cell && !cell.classList.contains('is-editing')) beginEdit(cell);
  });

  table.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter' && event.key !== ' ') return;

    const cell = (event.target as HTMLElement | null)?.closest<HTMLTableCellElement>('.admin-cell-edit');
    if (!cell || cell.classList.contains('is-editing')) return;

    event.preventDefault();
    beginEdit(cell);
  });
}
