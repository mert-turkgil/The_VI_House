/**
 * Upgrades every `<textarea data-ckeditor>` in the admin panel into a rich text editor — Journal
 * posts and seminar bodies alike.
 *
 * CKEditor is NOT bundled. It is loaded from CKEditor's CDN by _AdminLayout.cshtml and read here
 * off `window.CKEDITOR`, because a licence key is issued for a specific *distribution channel* and
 * ours covers "cloud". The npm build reports itself as "sh" (self-hosted):
 *
 *     const channel = window[Symbol.for('cke distribution')] || 'sh';
 *     if (key.distributionChannel && !key.distributionChannel.includes(channel)) → read-only editor
 *
 * which is why every editor in the panel silently became read-only while the key itself was
 * perfectly valid. The CDN bundle sets that symbol to "cloud" and the key validates. It also keeps
 * ~1 MB of editor out of admin.js.
 *
 * Progressive enhancement on purpose: the textarea is the real form field and stays in the DOM,
 * with CKEditor writing back into it on every change. If the CDN is unreachable the admin still
 * gets a working (plain) editor and can still publish — they just type HTML by hand.
 *
 * Image upload is switched on per-textarea by `data-upload-url`. Only the edit screens set it: on a
 * create screen there is no row yet for an asset to belong to, so media is added on the next screen.
 */

/** Only the members used below — the CDN bundle is untyped, and a hand-written mirror of CKEditor's
 *  full API would be a maintenance burden with no payoff. */
interface CKEditorInstance {
  getData(): string;
  model: {
    document: { on(event: string, callback: () => void): void };
    insertContent(content: unknown): void;
  };
  data: {
    processor: { toView(html: string): unknown };
    toModel(fragment: unknown): unknown;
  };
  editing: { view: { focus(): void } };
}

interface CKEditorNamespace {
  ClassicEditor: {
    create(element: HTMLElement, config: Record<string, unknown>): Promise<CKEditorInstance>;
  };
  [plugin: string]: unknown;
}

declare global {
  interface Window {
    CKEDITOR?: CKEditorNamespace;
  }
}

/**
 * Every live editor, keyed by the textarea it replaced. initMediaInsert needs to reach into the
 * body editor to insert an asset from the media library, and there is no other way back to the
 * instance once create() has resolved.
 */
const editors = new Map<HTMLTextAreaElement, CKEditorInstance>();

export function initRichTextEditors(): void {
  const textareas = Array.from(document.querySelectorAll<HTMLTextAreaElement>('textarea[data-ckeditor]'));
  if (textareas.length === 0) return;

  const ckeditor = window.CKEDITOR;
  if (!ckeditor) {
    console.error('CKEditor did not load from the CDN; falling back to plain textareas.');
    return;
  }

  const {
    ClassicEditor, Alignment, Autoformat, BlockQuote, Bold, Essentials, GeneralHtmlSupport, Heading,
    HorizontalLine, Image, ImageCaption, ImageResize, ImageStyle, ImageToolbar, ImageUpload, Italic,
    Link, List, MediaEmbed, Paragraph, PasteFromOffice, SimpleUploadAdapter, SourceEditing, Table,
    TableToolbar, Underline,
  } = ckeditor as CKEditorNamespace & { ClassicEditor: CKEditorNamespace['ClassicEditor'] };

  // The real key is only valid for the hosts it was issued for, so it comes from configuration
  // (rendered onto <body> by _AdminLayout) rather than being compiled in. An environment with none
  // configured falls back to 'GPL', which is the correct value for a GPL deployment.
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
        // Video embeds, and the general HTML support that lets an <audio> element inserted from the
        // media library survive a round trip through the editor's model — without it the model has
        // no rule for <audio> and silently drops it on the next save.
        MediaEmbed, GeneralHtmlSupport,
      ],
      toolbar: [
        'undo', 'redo', '|',
        'heading', '|',
        'bold', 'italic', 'underline', 'link', '|',
        'bulletedList', 'numberedList', 'blockQuote', '|',
        ...(uploadUrl ? ['uploadImage', '|'] : []),
        'mediaEmbed', '|',
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
      mediaEmbed: {
        // The saved HTML is <oembed url="…"> rather than a rendered <iframe>. That is the whole
        // point: nothing third-party is stored in the database, EditorHtml's no-iframe rule stays
        // intact, and the public page decides how to present it (a click-to-play facade — see
        // ArticleHtml on the server and modules/video.ts in the browser).
        previewsInData: false,
        // Replaces CKEditor's default provider list rather than subtracting from it, so YouTube is
        // the only URL the editor will accept as a video. The server refuses anything else anyway
        // (EditorHtml.NormaliseEmbeds) — this is so an admin finds out while pasting rather than
        // after saving and wondering where the embed went.
        providers: [
          {
            name: 'youtube',
            url: [
              /^(?:m\.)?youtube\.com\/watch\?v=([\w-]+)/,
              /^(?:m\.)?youtube\.com\/v\/([\w-]+)/,
              /^youtube\.com\/embed\/([\w-]+)/,
              /^youtube\.com\/shorts\/([\w-]+)/,
              /^youtu\.be\/([\w-]+)/,
            ],
          },
        ],
      },
      htmlSupport: {
        allow: [
          { name: 'audio', attributes: ['controls', 'src', 'preload'] },
          { name: 'source', attributes: ['src', 'type'] },
        ],
      },
      link: {
        // Journal posts and seminar notes routinely link off-site; opening those in a new tab keeps
        // the reader in the article, and the rel attributes are the standard safeguard with target.
        addTargetToExternalLinks: true,
      },
      ...(uploadUrl ? { simpleUpload: buildUploadConfig(textarea, uploadUrl) } : {}),
    })
      .then((editor) => {
        editors.set(textarea, editor);

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
 * Wires the media library's "Insert" buttons to the editor beside them.
 *
 * CKEditor has no audio plugin and no generic "insert this file" command, so an MP3 goes in as the
 * HTML it will finally be: the markup is parsed through the editor's own data pipeline rather than
 * poked into the textarea, so undo, source editing and the next save all behave normally.
 *
 * Each button carries `data-insert-html` (what to insert) and `data-insert-target` (the id of the
 * textarea whose editor should receive it).
 */
export function initMediaInsert(): void {
  const buttons = Array.from(document.querySelectorAll<HTMLElement>('[data-insert-html]'));
  if (buttons.length === 0) return;

  buttons.forEach((button) => {
    button.addEventListener('click', () => {
      const targetId = button.dataset.insertTarget;
      const textarea = targetId ? document.getElementById(targetId) : null;
      if (!(textarea instanceof HTMLTextAreaElement)) return;

      const editor = editors.get(textarea);
      const html = button.dataset.insertHtml ?? '';
      if (!html) return;

      if (!editor) {
        // No editor on this page (the CDN did not load). Append to the raw textarea instead, which
        // is exactly what an admin typing HTML by hand would do.
        textarea.value += html;
        return;
      }

      editor.model.insertContent(editor.data.toModel(editor.data.processor.toView(html)));
      editor.editing.view.focus();
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
