/**
 * Makes an admin table row clickable.
 *
 * Every list in the panel wraps only its first cell in an <a>, leaving about eighty per cent of
 * each row as dead pixels. This forwards a click anywhere on the row to the anchor that is already
 * there, rather than putting a handler on the <tr> and inventing a destination.
 *
 * That choice is what keeps it accessible. A <tr> cannot take focus and announces nothing, so
 * `onclick` on the row would be invisible to a keyboard and to a screen reader. Reusing the anchor
 * means the tab stop, the focus ring, the status-bar preview and the accessible name are all
 * already correct and unchanged — the row is just a bigger hit area for a link that was always
 * there.
 *
 * It also sidesteps two inconsistencies that would break a helper composing URLs from an id:
 * AdminCms links by slug while everything else links by Guid, and the Emails list has no detail
 * page at all. Reading the anchor handles both without a special case — a row with no anchor is
 * simply inert.
 */

/** Clicking these does something already; the row must not also navigate. */
const INTERACTIVE = 'a, button, input, select, textarea, label, form, [data-no-row-link]';

export function initRowLinks(): void {
  const tables = document.querySelectorAll<HTMLTableElement>('table.admin-table');
  if (tables.length === 0) return;

  tables.forEach((table) => {
    const body = table.tBodies[0];
    if (!body) return;

    // Delegated from the tbody, so rows re-rendered by a sort or a filter keep working without
    // rebinding — simple-datatables replaces row nodes when it re-orders them.
    body.addEventListener('click', (event) => {
      const target = event.target as HTMLElement | null;
      if (!target || target.closest(INTERACTIVE)) return;

      // Someone selecting an email address to copy is not trying to navigate.
      if (window.getSelection()?.toString()) return;

      const row = target.closest('tr');
      const link = row?.querySelector<HTMLAnchorElement>('a[href]');
      if (!link) return;

      // Modified clicks are left entirely alone, so Ctrl/Cmd/middle-click still open a new tab and
      // Shift still opens a window. Re-dispatching to the anchor is what preserves that for free.
      if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;

      event.preventDefault();
      link.click();
    });

    body.classList.add('admin-table__body--linked');
  });
}
