/**
 * Show/hide toggle for every password field on the site.
 *
 * One listener on document rather than one per button — Login, ResetPassword, ChangePassword,
 * SetPassword and DeletePersonalData each render their own [data-password-toggle], and delegation
 * means a page with three of them (ChangePassword) still costs one bound function.
 *
 * The button toggles its sibling <input>'s type between "password" and "text" and nothing else:
 * no re-render, no clearing the value, no touching the input's own event listeners — the field
 * keeps focus, selection and any unobtrusive-validation state exactly as it was.
 */
export function initPasswordToggle(): void {
  document.addEventListener('click', (event) => {
    const button = (event.target as HTMLElement).closest<HTMLButtonElement>('[data-password-toggle]');
    if (!button) return;

    const input = button.parentElement?.querySelector<HTMLInputElement>('input');
    if (!input) return;

    const revealed = input.type === 'text';
    input.type = revealed ? 'password' : 'text';

    button.classList.toggle('auth-password-toggle__btn--revealed', !revealed);
    button.setAttribute('aria-pressed', String(!revealed));
    button.setAttribute(
      'aria-label',
      (revealed ? button.dataset.showLabel : button.dataset.hideLabel) ?? button.getAttribute('aria-label') ?? '',
    );

    // Reappearing on the same spot the pointer already left it: the field the visitor was just
    // typing into is the one they want to keep looking at, not the button.
    input.focus({ preventScroll: true });
  });
}
