/**
 * Adds a "download as .txt" button to the one-time recovery-codes page.
 *
 * The codes are read back out of the DOM rather than fetched, so the server hands out the plaintext
 * set exactly once — the same request that rendered them. The button is created here instead of
 * living in the Razor markup so it can never appear as a dead control when this bundle fails to
 * load; the codes themselves are always on screen to copy by hand, which is the real fallback.
 *
 * Its label and filename come from data attributes rather than string literals, because this page
 * is localised (EN/DE/TR/ET) and the bundle has no access to the resource files.
 */
export function initRecoveryCodeDownload(): void {
  const actions = document.querySelector<HTMLElement>('[data-recovery-download]');
  const list = document.querySelector<HTMLElement>('[data-recovery-codes]');
  if (!actions || !list) return;

  const codes = Array.from(list.querySelectorAll('code'))
    .map((el) => el.textContent?.trim() ?? '')
    .filter((text) => text.length > 0);

  if (codes.length === 0) return;

  const label = actions.dataset.recoveryDownload ?? 'Download codes';
  const filename = actions.dataset.recoveryFilename ?? 'vi-house-recovery-codes.txt';

  const button = document.createElement('button');
  button.type = 'button';
  button.className = 'btn btn--ghost onboarding__codes-download';
  button.textContent = label;

  button.addEventListener('click', () => {
    const body = [
      'The VI House — two-factor recovery codes',
      `Generated ${new Date().toISOString()}`,
      '',
      'Each code works once. Keep them somewhere you can reach without your phone.',
      '',
      ...codes,
      '',
    ].join('\r\n');

    const url = URL.createObjectURL(new Blob([body], { type: 'text/plain;charset=utf-8' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    link.click();

    // Revoking immediately would race the download in some browsers; a tick is enough for the
    // navigation to have taken the blob.
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  });

  actions.appendChild(button);
}
