/**
 * Counts the homepage stat figures up from zero when they scroll into view.
 *
 * Values are CMS-authored free text (`ContentBlock.ExtraJson`, editable under Admin → Content), so
 * they can be anything — "180+", "24", or a dash placeholder. Only the numeric part is animated and
 * any surrounding characters are preserved verbatim, which means an editor can type whatever reads
 * best without the animation mangling it, and a non-numeric value simply animates nothing.
 */

const DURATION_MS = 1400;

// Decelerating curve: fast off the mark, easing into the final figure. Reads as deliberate rather
// than mechanical, which a linear ramp does not.
const easeOutCubic = (t: number): number => 1 - Math.pow(1 - t, 3);

function animateValue(el: HTMLElement): void {
  const raw = el.textContent?.trim() ?? '';
  const match = raw.match(/\d[\d.,]*/);
  if (!match) return;

  const numericText = match[0];
  const target = Number(numericText.replace(/[.,]/g, ''));
  if (!Number.isFinite(target) || target === 0) return;

  const prefix = raw.slice(0, match.index ?? 0);
  const suffix = raw.slice((match.index ?? 0) + numericText.length);

  // Reserve the final width up front so neighbouring stats don't shuffle sideways as the digits
  // grow — the row is a flex layout and would otherwise visibly reflow throughout the count.
  el.style.minWidth = `${el.getBoundingClientRect().width}px`;

  const start = performance.now();

  const step = (now: number) => {
    const progress = Math.min((now - start) / DURATION_MS, 1);
    const current = Math.round(target * easeOutCubic(progress));
    el.textContent = `${prefix}${current.toLocaleString('en-GB')}${suffix}`;

    if (progress < 1) {
      requestAnimationFrame(step);
    } else {
      el.textContent = raw; // land exactly on the authored text, formatting and all
    }
  };

  requestAnimationFrame(step);
}

export function initStatCounters(): void {
  const values = Array.from(document.querySelectorAll<HTMLElement>('[data-count-up]'));
  if (values.length === 0) return;

  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches || !('IntersectionObserver' in window)) {
    return; // the authored value is already in the DOM; leaving it alone is the correct fallback
  }

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;
        animateValue(entry.target as HTMLElement);
        observer.unobserve(entry.target);
      });
    },
    { threshold: 0.6 },
  );

  values.forEach((el) => observer.observe(el));
}
