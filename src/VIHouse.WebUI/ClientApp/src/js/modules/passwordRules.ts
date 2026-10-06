/**
 * Live password checklist (Views/Shared/_PasswordRules.cshtml).
 *
 * Ticks each rule as the reader types, so the rules are known before the first submit rather than
 * revealed one refusal at a time. When the form is submitted with rules still unmet, those rules
 * turn red: the error and the rule it is about appear in the same place.
 *
 * The tests mirror ASP.NET Core Identity's PasswordValidator exactly, which is ASCII-based: an
 * uppercase letter means A–Z, and a "symbol" is anything outside A–Z, a–z and 0–9 (so "ş" counts as a
 * symbol there, and does here too). The server stays the authority. Submit is never blocked by this
 * module; it only says in advance what the server will ask for.
 */
const TESTS: Record<string, (value: string, min: number) => boolean> = {
  length: (value, min) => value.length >= min,
  upper: (value) => /[A-Z]/.test(value),
  lower: (value) => /[a-z]/.test(value),
  digit: (value) => /[0-9]/.test(value),
  symbol: (value) => /[^A-Za-z0-9]/.test(value),
};

export function initPasswordRules(): void {
  document.querySelectorAll<HTMLElement>('[data-password-rules]').forEach(setUp);
}

function setUp(box: HTMLElement): void {
  const password = document.getElementById(box.dataset.for ?? '') as HTMLInputElement | null;
  const confirm = box.dataset.confirm ? (document.getElementById(box.dataset.confirm) as HTMLInputElement | null) : null;
  if (!password) return;

  const min = Number(box.dataset.min ?? '10');
  const metLabel = box.dataset.met ?? '';
  const unmetLabel = box.dataset.unmet ?? '';
  const items = Array.from(box.querySelectorAll<HTMLElement>('[data-rule]'));

  const evaluate = (): boolean => {
    let allMet = true;
    items.forEach((item) => {
      const rule = item.dataset.rule ?? '';
      const met = rule === 'match'
        ? !!confirm && confirm.value.length > 0 && confirm.value === password.value
        : TESTS[rule]?.(password.value, min) ?? true;

      if (met !== item.classList.contains('is-met')) {
        item.classList.toggle('is-met', met);
        const state = item.querySelector<HTMLElement>('[data-rule-state]');
        if (state) state.textContent = met ? metLabel : unmetLabel;
      }
      // A rule fixed after a failed submit stops being red straight away.
      if (met) item.classList.remove('is-error');
      allMet &&= met;
    });

    box.classList.toggle('is-touched', password.value.length > 0);
    box.classList.toggle('is-complete', allMet);
    return allMet;
  };

  password.addEventListener('input', evaluate);
  confirm?.addEventListener('input', evaluate);

  password.form?.addEventListener('submit', () => {
    if (evaluate()) return;
    items.filter((item) => !item.classList.contains('is-met')).forEach((item) => item.classList.add('is-error'));
    // Restart the nudge animation even when the same rules fail twice in a row.
    box.classList.remove('is-attempted');
    void box.offsetWidth;
    box.classList.add('is-attempted');
  });

  // A page re-rendered after a failed POST keeps the typed value only in the browser's memory for
  // some password managers; evaluate whatever is there now.
  evaluate();
}
