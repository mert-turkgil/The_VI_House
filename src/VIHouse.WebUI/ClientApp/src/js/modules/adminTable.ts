/**
 * Sorting, instant search and paging for the admin lists.
 *
 * simple-datatables rather than jQuery DataTables, even though jQuery is already on the page for
 * the unobtrusive-validation scripts fifteen admin forms rely on. Binding a table library to that
 * incidental global would make a copy-editing tool depend on a validation dependency; this one is
 * vanilla and ignores `$` entirely.
 *
 * The reason it is safe to let a library re-render these rows: several lists have POST forms in
 * their cells (delete buttons carrying an antiforgery token). simple-datatables models a cell as a
 * node tree with attributes and form state, and patches it through diff-dom rather than assigning
 * innerHTML, so those forms survive a sort intact.
 */
import { DataTable } from 'simple-datatables';
import autoAnimate from '@formkit/auto-animate';

/** Below this a table is short enough that search and paging are just clutter. */
const MIN_ROWS_FOR_FEATURES = 8;

export function initAdminTables(): void {
  const tables = document.querySelectorAll<HTMLTableElement>('table[data-admin-table]');
  if (tables.length === 0) return;

  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  tables.forEach((table) => {
    const rows = table.tBodies[0]?.rows.length ?? 0;
    const sortable = table.dataset.adminTableSort !== 'false';
    const perPage = Number(table.dataset.adminTablePerPage ?? '25');

    const small = rows < MIN_ROWS_FOR_FEATURES;

    const dataTable = new DataTable(table, {
      sortable: sortable && !small,
      searchable: !small,
      paging: !small,
      perPage,
      perPageSelect: small ? false : [25, 50, 100, 250],
      labels: {
        placeholder: 'Search…',
        perPage: 'per page',
        noRows: 'Nothing here yet.',
        noResults: 'Nothing matches that search.',
        info: '{start}–{end} of {rows}',
      },
      classes: {
        wrapper: 'admin-dt',
        search: 'admin-dt__search',
        input: 'admin-dt__input',
        top: 'admin-dt__top',
        bottom: 'admin-dt__bottom',
        selector: 'admin-dt__selector',
        paginationList: 'admin-dt__pages',
      },
    });

    // Rows sliding in and out as a search narrows, rather than snapping. Skipped outright under
    // prefers-reduced-motion — the house rule is that the animation never starts, not that it runs
    // faster (see reveal.ts).
    if (!reduced) {
      dataTable.on('datatable.init', () => {
        const body = table.tBodies[0];
        if (body) autoAnimate(body, { duration: 180, easing: 'ease-out' });
      });
    }
  });
}
