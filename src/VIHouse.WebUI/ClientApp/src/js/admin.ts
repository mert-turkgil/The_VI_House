import '../scss/admin.scss';
import { initRichTextEditors, initMediaInsert } from './modules/editor';
import { initAdminCharts } from './modules/charts';
import { initAdminTables } from './modules/adminTable';
import { initRowLinks } from './modules/adminRows';
import { initAdminNav } from './modules/adminNav';
import { initToasts } from './modules/toasts';
import { initTranslationEditor } from './modules/translations';
import { initScrollReveal } from './modules/reveal';

// Every init is called unconditionally and guards itself on the data attribute or element it needs,
// which is the convention both entry points already follow — there is no page router.
//
// Order matters in one place: initAdminTables must run before initRowLinks, because the table
// library re-parents the rows the row handler delegates from.
document.addEventListener('DOMContentLoaded', () => {
  initAdminNav();
  initToasts();
  initRichTextEditors();
  initMediaInsert();
  initAdminCharts();
  initAdminTables();
  initRowLinks();
  initTranslationEditor();
  initScrollReveal();
});
