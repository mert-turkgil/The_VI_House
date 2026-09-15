/**
 * Copy-to-clipboard buttons: `<button data-copy="text" data-copied-label="Copied">`. The button's
 * label swaps to the copied text for a moment and then back. Without the Clipboard API (a plain
 * http origin, an old browser) the button does nothing visible — the value is printed beside it
 * anyway, so selecting it by hand still works.
 */
export function initCopyButtons(): void {
  const buttons = Array.from(document.querySelectorAll<HTMLElement>('[data-copy]'));
  if (buttons.length === 0 || !navigator.clipboard) return;

  buttons.forEach((button) => {
    const original = button.textContent ?? '';
    const activate = async () => {
      const value = button.dataset.copy ?? '';
      try {
        await navigator.clipboard.writeText(value);
      } catch {
        return;
      }
      button.textContent = button.dataset.copiedLabel ?? 'Copied';
      button.classList.add('is-copied');
      window.setTimeout(() => {
        button.textContent = original;
        button.classList.remove('is-copied');
      }, 1800);
    };
    button.addEventListener('click', activate);
    // A non-button element carrying data-copy (a <code> with role="button") needs the keyboard path too.
    if (button.tagName !== 'BUTTON') {
      button.addEventListener('keydown', (event) => {
        if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); void activate(); }
      });
    }
  });
}
