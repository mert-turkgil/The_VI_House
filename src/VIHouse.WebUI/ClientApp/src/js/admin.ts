// Admin panel bundle — loaded only by Areas/Admin/Views/Shared/_AdminLayout.cshtml, alongside (not
// instead of) the shared main.css. Everything heavy and admin-only lives here: CKEditor for writing
// Journal posts and seminar bodies, Chart.js for the dashboard. Keeping it out of main.ts is the whole point of the
// two-entry Vite build — a homepage visitor should never download an editor they can't open.
import '../scss/admin.scss';
import { initRichTextEditors, initMediaInsert } from './modules/editor';
import { initAdminCharts } from './modules/charts';

document.addEventListener('DOMContentLoaded', () => {
  initRichTextEditors();
  initMediaInsert();
  initAdminCharts();
});
