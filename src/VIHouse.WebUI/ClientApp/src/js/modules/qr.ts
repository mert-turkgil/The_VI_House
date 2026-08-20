/**
 * Renders the authenticator QR code on the onboarding two-factor page.
 *
 * The library is pulled in with a dynamic import so it lands in its own chunk: this is the one page
 * on the whole site that needs it, and every other visitor would otherwise pay for it on first load.
 *
 * If it fails to load, the page is still usable — the same secret is printed underneath as text for
 * manual entry, which is why that fallback is never hidden behind the QR code.
 */
export function initAuthenticatorQr(): void {
  const target = document.querySelector<HTMLElement>('[data-qr-uri]');
  if (!target) return;

  const uri = target.dataset.qrUri;
  if (!uri) return;

  import('qrcode')
    .then(({ default: QRCode }) => {
      const canvas = document.createElement('canvas');
      canvas.setAttribute('role', 'img');
      canvas.setAttribute('aria-label', 'QR code for your authenticator app');
      target.appendChild(canvas);

      return QRCode.toCanvas(canvas, uri, {
        width: 200,
        margin: 1,
        // Dark on cream rather than pure black on white, to sit with the rest of the page without
        // dropping below the contrast a scanner needs.
        color: { dark: '#00230a', light: '#ffffff' },
      });
    })
    .catch(() => {
      target.textContent = 'Couldn’t draw the QR code — use the setup key below instead.';
      target.classList.add('onboarding-qr--failed');
    });
}
