import {
  ClassicEditor,
  Alignment,
  Autoformat,
  BlockQuote,
  Bold,
  Essentials,
  Heading,
  HorizontalLine,
  Italic,
  Link,
  List,
  Paragraph,
  PasteFromOffice,
  SourceEditing,
  Table,
  TableToolbar,
  Underline,
} from 'ckeditor5';
import 'ckeditor5/ckeditor5.css';

/**
 * Upgrades the Journal body `<textarea data-ckeditor>` into a rich text editor.
 *
 * Progressive enhancement on purpose: the textarea is the real form field and stays in the DOM,
 * with CKEditor writing back into it on every change. If this bundle fails to load, the admin still
 * gets a working (plain) editor and can still publish — they just type HTML by hand rather than
 * seeing a toolbar.
 */
export function initJournalEditor(): void {
  const textareas = Array.from(document.querySelectorAll<HTMLTextAreaElement>('textarea[data-ckeditor]'));
  if (textareas.length === 0) return;

  textareas.forEach((textarea) => {
    ClassicEditor.create(textarea, {
      // Required by CKEditor 5 to run under its GPL terms without a commercial licence key.
      licenseKey: 'GPL',
      plugins: [
        Essentials, Paragraph, Heading, Bold, Italic, Underline, Link, List,
        BlockQuote, Table, TableToolbar, Alignment, HorizontalLine, Autoformat,
        PasteFromOffice, SourceEditing,
      ],
      toolbar: [
        'undo', 'redo', '|',
        'heading', '|',
        'bold', 'italic', 'underline', 'link', '|',
        'bulletedList', 'numberedList', 'blockQuote', '|',
        'alignment', 'insertTable', 'horizontalLine', '|',
        'sourceEditing',
      ],
      heading: {
        options: [
          { model: 'paragraph', title: 'Paragraph', class: 'ck-heading_paragraph' },
          { model: 'heading2', view: 'h2', title: 'Heading', class: 'ck-heading_heading2' },
          { model: 'heading3', view: 'h3', title: 'Subheading', class: 'ck-heading_heading3' },
        ],
      },
      table: { contentToolbar: ['tableColumn', 'tableRow', 'mergeTableCells'] },
      link: {
        // Journal posts routinely link off-site; opening those in a new tab keeps the reader in
        // the post, and the rel attributes are the standard safeguard that comes with target.
        addTargetToExternalLinks: true,
      },
    })
      .then((editor) => {
        // ClassicEditor.create on a <textarea> already syncs back on form submit, but the admin
        // forms are validated by jQuery unobtrusive validation, which reads the field's value
        // before submit fires — without this the [Required] check sees the original (possibly
        // empty) textarea and blocks a perfectly valid post.
        editor.model.document.on('change:data', () => {
          textarea.value = editor.getData();
        });
      })
      .catch((error: unknown) => {
        // Leaving the plain textarea in place is a working fallback, so this must never take the
        // page down with it — but it should be visible to whoever is debugging.
        console.error('Rich text editor failed to load; falling back to plain textarea.', error);
      });
  });
}
