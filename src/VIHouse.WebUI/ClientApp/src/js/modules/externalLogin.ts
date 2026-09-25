/**
 * Loading state for the "Continue with Google" button on Login.
 *
 * Submitting #external-account is a real form post that leaves the page (it 302s out to Google's
 * consent screen), so there is no completion event to reset the button on — the swap only ever
 * needs to happen once, on click, and can stay swapped until the browser navigates away.
 */
export function initExternalLogin(): void {
  const form = document.querySelector<HTMLFormElement>('#external-account');
  if (!form) return;

  form.addEventListener('submit', () => {
    const button = form.querySelector<HTMLButtonElement>('button[type="submit"]');
    if (!button || button.getAttribute('aria-busy') === 'true') return;

    // Not button.disabled = true: a disabled control is excluded from the form's own data set at
    // submission (it's how the browser decides which button's name=value pair to send), so
    // disabling THIS button strips provider=Google from the very request already in flight and
    // the server falls back to challenging its default auth scheme instead of Google. aria-busy
    // plus the CSS pointer-events:none on the button (see _auth.scss) blocks a second click
    // without touching what already left the browser.
    button.setAttribute('aria-busy', 'true');
    // The label stays in the accessible tree (visually hidden) rather than being replaced outright,
    // so a screen reader announces "Redirecting…" instead of falling silent mid-action.
    button.innerHTML = `
      <span class="auth-external__loader" aria-hidden="true">
        <span class="auth-external__loader-dot auth-external__loader-dot--blue"></span>
        <span class="auth-external__loader-dot auth-external__loader-dot--red"></span>
        <span class="auth-external__loader-dot auth-external__loader-dot--yellow"></span>
        <span class="auth-external__loader-dot auth-external__loader-dot--green"></span>
      </span>
      <span class="visually-hidden">${button.dataset.loadingLabel ?? 'Redirecting…'}</span>
    `;
  });
}
