/**
 * Loading state for the "Continue with Google / Apple" buttons on Login.
 *
 * Submitting #external-account is a real form post that leaves the page (it 302s out to Google's
 * consent screen), so there is no completion event to reset the button on — the swap only ever
 * needs to happen once, on click, and can stay swapped until the browser navigates away.
 */
export function initExternalLogin(): void {
  const form = document.querySelector<HTMLFormElement>('#external-account');
  if (!form) return;

  form.addEventListener('submit', (event: SubmitEvent) => {
    // The button actually pressed — with Google and Apple side by side, the first button in the
    // form is not necessarily the one that was clicked.
    const button = (event.submitter as HTMLButtonElement | null)
      ?? form.querySelector<HTMLButtonElement>('button[type="submit"]');
    if (!button || button.getAttribute('aria-busy') === 'true') return;
    // Google's four colours for Google; a plain monochrome bounce for everyone else (Apple's
    // guidelines keep its button black-and-white).
    const dots = button.value.toLowerCase() === 'google'
      ? ['blue', 'red', 'yellow', 'green']
      : ['mono', 'mono', 'mono', 'mono'];

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
        ${dots.map((c, i) => `<span class="auth-external__loader-dot auth-external__loader-dot--${c}" style="animation-delay:${i * 0.1}s"></span>`).join('')}
      </span>
      <span class="visually-hidden">${button.dataset.loadingLabel ?? 'Redirecting…'}</span>
    `;
  });
}
