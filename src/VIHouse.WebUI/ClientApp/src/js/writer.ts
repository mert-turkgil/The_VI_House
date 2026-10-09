import '../scss/writer.scss';
import { initRichTextEditors } from './modules/editor';
import { initJournalWriter } from './modules/journalWriter';

// The influencer's article writer (Views/Influencer/Write), loaded on that page only, after main.js:
// the same editor and writer the admin panel uses, without the rest of the panel. CKEditor itself
// comes from the CDN (Shared/_CkEditorAssets), as it does in the panel.
document.addEventListener('DOMContentLoaded', () => {
  initRichTextEditors();
  initJournalWriter();
});
