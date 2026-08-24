import {
  ClassicEditor,
  Alignment,
  Autoformat,
  BlockQuote,
  Bold,
  Essentials,
  Heading,
  HorizontalLine,
  Image,
  ImageCaption,
  ImageResize,
  ImageStyle,
  ImageToolbar,
  ImageUpload,
  Italic,
  Link,
  List,
  Paragraph,
  PasteFromOffice,
  SimpleUploadAdapter,
  SourceEditing,
  Table,
  TableToolbar,
  Underline,
} from 'ckeditor5';
import 'ckeditor5/ckeditor5.css';

/**
 * Upgrades every `<textarea data-ckeditor>` in the admin panel into a rich text editor — Journal
 * posts and seminar bodies alike.
 *
 * Progressive enhancement on purpose: the textarea is the real form field and stays in the DOM,
 * with CKEditor writing back into it on every change. If this bundle fails to load, the admin still
 * gets a working (plain) editor and can still publish — they just type HTML by hand rather than
 * seeing a toolbar.
 *
 * Image upload is switched on per-textarea by `data-upload-url`. Only the seminar editor sets it:
 * on the Create screen there is no seminar row yet for an asset to belong to, so images are added
 * from the media library on the next screen instead.
 */
export function initRichTextEditors(): void {
  const textareas = Array.from(document.querySelectorAll<HTMLTextAreaElement>('textarea[data-ckeditor]'));
  if (textareas.length === 0) return;

  // CKEditor 5 requires a licence key even to run under its GPL terms. The real key is only valid
  // for the hosts it was issued for, so it comes from configuration (rendered onto <body> by
  // _AdminLayout) rather than being compiled in — an environment with none configured falls back to
  // 'GPL', which is the correct value for a GPL deployment and keeps the editor working.
  const licenseKey = document.body.dataset.ckeditorLicense || 'GPL';

  textareas.forEach((textarea) => {
    const uploadUrl = textarea.dataset.uploadUrl;

    ClassicEditor.create(textarea, {
      licenseKey,
      plugins: [
        Essentials, Paragraph, Heading, Bold, Italic, Underline, Link, List,
        BlockQuote, Table, TableToolbar, Alignment, HorizontalLine, Autoformat,
        PasteFromOffice, SourceEditing,
        // Images are always available so pasted/inserted markup renders; the *upload* button is
        // only added to the toolbar below when there is somewhere to upload to.
        Image, ImageToolbar, ImageCaption, ImageStyle, ImageResize, ImageUpload, SimpleUploadAdapter,
      ],
      toolbar: [
        'undo', 'redo', '|',
        'heading', '|',
        'bold', 'italic', 'underline', 'link', '|',
        'bulletedList', 'numberedList', 'blockQuote', '|',
        ...(uploadUrl ? ['uploadImage', '|'] : []),
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
      image: {
        toolbar: [
          'imageTextAlternative', '|',
          'imageStyle:inline', 'imageStyle:block', 'imageStyle:side', '|',
          'toggleImageCaption',
        ],
      },
      table: { contentToolbar: ['tableColumn', 'tableRow', 'mergeTableCells'] },
      link: {
        // Journal posts and seminar notes routinely link off-site; opening those in a new tab keeps
        // the reader in the article, and the rel attributes are the standard safeguard with target.
        addTargetToExternalLinks: true,
      },
      ...(uploadUrl ? { simpleUpload: buildUploadConfig(textarea, uploadUrl) } : {}),
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

/**
 * The editor posts uploads itself, outside any `<form>`, so the antiforgery token has to travel as
 * a header — ASP.NET Core's AutoValidateAntiforgeryToken accepts `RequestVerificationToken`
 * alongside the usual hidden field. The token is lifted from any form already on the page rather
 * than rendered a second time just for this.
 */
function buildUploadConfig(textarea: HTMLTextAreaElement, uploadUrl: string) {
  const fieldName = textarea.dataset.uploadTokenField || '__RequestVerificationToken';
  const tokenInput = document.querySelector<HTMLInputElement>(`input[name="${fieldName}"]`);

  return {
    uploadUrl,
    withCredentials: true,
    headers: tokenInput ? { RequestVerificationToken: tokenInput.value } : {},
  };
}
