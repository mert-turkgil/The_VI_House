/**
 * The digital membership card (Views/Account/Card.cshtml).
 *
 * - With a mouse or trackpad the card tilts towards the pointer, and a foil glare follows it. Set
 *   through CSS custom properties, read by _auth.scss, and written at most once a frame.
 * - The flip button turns the card over to the back face (the member number, large); on touch
 *   screens, tapping the card does the same. The button is hidden in the markup and only revealed
 *   here, so it never appears where it cannot work.
 *
 * Under prefers-reduced-motion nothing tilts, and the flip is an instant swap (the stylesheet drops
 * the transition).
 */
export function initMemberCard(): void {
  const stage = document.querySelector<HTMLElement>('[data-member-pass]');
  const card = stage?.querySelector<HTMLElement>('.member-pass');
  if (!stage || !card) return;

  const button = document.querySelector<HTMLButtonElement>('[data-member-card-flip]');
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const finePointer = window.matchMedia('(hover: hover) and (pointer: fine)').matches;

  const setFlipped = (flipped: boolean): void => {
    card.classList.toggle('is-flipped', flipped);
    card.querySelector('.member-pass__face--front')?.setAttribute('aria-hidden', String(flipped));
    card.querySelector('.member-pass__face--back')?.setAttribute('aria-hidden', String(!flipped));
    if (button) {
      button.setAttribute('aria-pressed', String(flipped));
      button.textContent = (flipped ? button.dataset.labelFront : button.dataset.labelBack) ?? button.textContent;
    }
  };

  if (button) {
    button.hidden = false;
    button.addEventListener('click', () => setFlipped(!card.classList.contains('is-flipped')));
  }

  if (!finePointer) {
    // Touch: a tap on the card turns it over.
    card.addEventListener('click', () => setFlipped(!card.classList.contains('is-flipped')));
    return;
  }

  if (prefersReducedMotion) return;

  let frame = 0;
  let pointerX = 0;
  let pointerY = 0;
  stage.addEventListener('pointermove', (event) => {
    // The latest position wins: the frame reads these, not the event that scheduled it.
    pointerX = event.clientX;
    pointerY = event.clientY;
    if (frame) return;
    frame = requestAnimationFrame(() => {
      frame = 0;
      const box = card.getBoundingClientRect();
      const x = Math.min(1, Math.max(0, (pointerX - box.left) / box.width));
      const y = Math.min(1, Math.max(0, (pointerY - box.top) / box.height));
      card.style.setProperty('--tilt-x', `${((0.5 - y) * 10).toFixed(2)}deg`);
      card.style.setProperty('--tilt-y', `${((x - 0.5) * 14).toFixed(2)}deg`);
      card.style.setProperty('--glare-x', `${(x * 100).toFixed(1)}%`);
      card.style.setProperty('--glare-y', `${(y * 100).toFixed(1)}%`);
      card.classList.add('is-tilting');
    });
  });

  stage.addEventListener('pointerleave', () => {
    cancelAnimationFrame(frame);
    frame = 0;
    card.classList.remove('is-tilting');
    card.style.removeProperty('--tilt-x');
    card.style.removeProperty('--tilt-y');
  });
}
