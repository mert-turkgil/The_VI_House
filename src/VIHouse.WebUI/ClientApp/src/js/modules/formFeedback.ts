/**
 * Makes a form's errors impossible to miss and easy to act on (Views/Shared/_ErrorSummary.cshtml).
 *
 * - After a failed POST the error summary is focused, so it is the first thing seen, and the first
 *   thing a screen reader announces.
 * - Each summary item links to its field; following the link focuses the field itself, not just
 *   the top of the page at that anchor.
 * - Every field the server marked invalid gets aria-invalid, and is described by its own message,
 *   so a screen reader reads the error when the field is reached.
 *
 * jQuery Unobtrusive Validation already does the aria part for client-side errors; this covers the
 * server-rendered ones, which it never sees.
 */
export function initFormFeedback(): void {
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  document.querySelectorAll<HTMLInputElement>('input.input-validation-error, select.input-validation-error, textarea.input-validation-error')
    .forEach((field) => {
      field.setAttribute('aria-invalid', 'true');
      const message = field.form?.querySelector<HTMLElement>(`[data-valmsg-for="${CSS.escape(field.name)}"]`);
      if (!message || !field.id) return;
      message.id ||= `${field.id}-error`;
      const describedBy = new Set((field.getAttribute('aria-describedby') ?? '').split(' ').filter(Boolean));
      describedBy.add(message.id);
      field.setAttribute('aria-describedby', Array.from(describedBy).join(' '));
    });

  const summary = document.querySelector<HTMLElement>('[data-error-summary]');
  if (!summary) return;

  summary.focus({ preventScroll: true });
  summary.scrollIntoView({ block: 'center', behavior: prefersReducedMotion ? 'auto' : 'smooth' });

  summary.addEventListener('click', (event) => {
    const link = (event.target as HTMLElement).closest<HTMLAnchorElement>('a[data-error-link]');
    if (!link) return;
    const target = document.getElementById(decodeURIComponent(link.hash.slice(1)));
    if (!target) return;
    event.preventDefault();
    target.scrollIntoView({ block: 'center', behavior: prefersReducedMotion ? 'auto' : 'smooth' });
    target.focus({ preventScroll: true });
  });
}
