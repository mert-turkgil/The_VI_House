/**
 * The tracked-link builder on the ambassador admin page: `[data-utm-builder][data-utm-base]` with
 * `[data-utm="source|medium|campaign"]` inputs, a `[data-utm-preview]` element and a
 * `[data-utm-copy]` button. The preview and the copy button's value follow the inputs as they are
 * typed; blank tags are left out so the plain link stays plain.
 */
export function initUtmBuilder(): void {
  const builders = Array.from(document.querySelectorAll<HTMLElement>('[data-utm-builder]'));
  builders.forEach((builder) => {
    const base = builder.dataset.utmBase ?? '';
    const inputs = Array.from(builder.querySelectorAll<HTMLInputElement>('[data-utm]'));
    const preview = builder.querySelector<HTMLElement>('[data-utm-preview]');
    const copy = builder.querySelector<HTMLElement>('[data-utm-copy]');
    if (!base || inputs.length === 0 || !preview) return;

    const render = () => {
      const url = new URL(base);
      inputs.forEach((input) => {
        const key = `utm_${input.dataset.utm}`;
        // Lower-cased and space-free: a tag that reads "Instagram Story" would fragment the
        // source table into as many rows as there are ways to type it.
        const value = input.value.trim().toLowerCase().replace(/\s+/g, '-');
        if (value) url.searchParams.set(key, value);
      });
      const text = url.toString();
      preview.textContent = text;
      if (copy) copy.dataset.copy = text;
    };

    inputs.forEach((input) => input.addEventListener('input', render));
    render();
  });
}
