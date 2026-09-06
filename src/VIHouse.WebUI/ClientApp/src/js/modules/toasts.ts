/**
 * Toast notifications for the admin panel.
 *
 * Two jobs. The first is to upgrade the status banner the server already renders: every admin
 * controller sets TempData["StatusMessage"] and every view prints it into `.admin-status-message`.
 * Rather than edit thirty views, this finds that element and re-presents it as a toast. With
 * scripting off the banner simply stays where it is and reads exactly as before — the toast is an
 * enhancement, never the mechanism, the same rule reveal.ts follows.
 *
 * The second is `toast()`, for the parts of the panel that save without a page load (the
 * translations editor) and therefore have no server-rendered banner to upgrade.
 */
import { Notyf } from 'notyf';
import 'notyf/notyf.min.css';

let instance: Notyf | null = null;

function notyf(): Notyf {
  if (instance) return instance;

  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  instance = new Notyf({
    // Long enough to read a sentence, which is what these messages are — the framework default of
    // 2s is tuned for "Saved" and clips anything with a reason in it.
    duration: 4500,
    ripple: false,
    dismissible: true,
    position: { x: 'right', y: 'bottom' },
    types: [
      { type: 'success', background: 'var(--vih-green-900)', icon: false },
      // Errors carry the reason a save was refused, so they wait for a click rather than expiring.
      { type: 'error', background: 'var(--vih-danger)', icon: false, duration: reduced ? 6000 : 0 },
    ],
  });

  return instance;
}

export function toast(message: string, kind: 'success' | 'error' = 'success'): void {
  notyf().open({ type: kind, message });
}

export function initToasts(): void {
  const banner = document.querySelector<HTMLElement>('.admin-status-message');
  if (!banner) return;

  const message = banner.textContent?.trim();
  if (!message) return;

  // A banner containing a link is left alone: the message is asking the reader to go somewhere
  // ("2 emails failed to send. Show them."), and a toast that disappears takes the link with it.
  if (banner.querySelector('a')) return;

  toast(message, /fail|error|could not|cannot|refus/i.test(message) ? 'error' : 'success');
  banner.remove();
}
