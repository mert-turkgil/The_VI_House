import Swiper from 'swiper';
import { Navigation, Pagination, A11y, Keyboard } from 'swiper/modules';

/**
 * The join-and-pay page (Views/Join/Index.cshtml).
 *
 * Everything here is an enhancement over a form that already works on its own: the plan cards
 * are radio buttons in a row, the summary panel is rendered for every plan with all but the chosen
 * one hidden, and the submit button submits. What the script adds is the feel of a checkout —
 * cards that snap and centre, a summary that follows the choice, step numbers that tick as the
 * form fills in, and a button that says what it is doing once pressed.
 */
export function initJoinPage(): void {
  const root = document.querySelector<HTMLElement>('[data-join]');
  if (!root) return;

  const form = root.querySelector<HTMLFormElement>('[data-join-form]');
  if (!form) return;

  const radios = Array.from(form.querySelectorAll<HTMLInputElement>('input[name="PlanId"]'));
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // --- Plan cards -----------------------------------------------------------------------------
  const strip = root.querySelector<HTMLElement>('[data-plan-swiper]');
  let swiper: Swiper | null = null;

  if (strip) {
    const scope = strip.closest<HTMLElement>('[data-swiper-scope]');
    const cluster = scope?.querySelector<HTMLElement>('[data-swiper-controls]') ?? null;
    const checkedIndex = Math.max(0, radios.findIndex((r) => r.checked));

    swiper = new Swiper(strip, {
      modules: [Navigation, Pagination, A11y, Keyboard],
      // The cards set their own width in CSS; Swiper only has to snap between them. 'auto' is
      // what lets two plans sit side by side on a wide screen and four scroll, with one config.
      slidesPerView: 'auto',
      spaceBetween: 16,
      speed: prefersReducedMotion ? 0 : 550,
      grabCursor: true,
      watchOverflow: true,
      watchSlidesProgress: true,
      keyboard: { enabled: true, onlyInViewport: true },
      initialSlide: checkedIndex,
      // A tap on a card that is half off the edge brings it in rather than leaving the reader to
      // choose something they cannot fully see.
      slideToClickedSlide: true,
      navigation: {
        prevEl: cluster?.querySelector<HTMLElement>('[data-swiper-prev]') ?? null,
        nextEl: cluster?.querySelector<HTMLElement>('[data-swiper-next]') ?? null,
      },
      pagination: {
        el: cluster?.querySelector<HTMLElement>('[data-swiper-pagination]') ?? null,
        type: 'progressbar',
      },
      a11y: {
        prevSlideMessage: strip.dataset.swiperPrevLabel || 'Previous slide',
        nextSlideMessage: strip.dataset.swiperNextLabel || 'Next slide',
      },
      on: {
        init: (s) => { if (cluster) cluster.hidden = s.isLocked; },
        lock: () => { if (cluster) cluster.hidden = true; },
        unlock: () => { if (cluster) cluster.hidden = false; },
      },
    });
  }

  // --- Summary panel --------------------------------------------------------------------------
  const summary = root.querySelector<HTMLElement>('[data-join-summary]');
  const none = summary?.querySelector<HTMLElement>('[data-summary-none]') ?? null;
  const planPanels = Array.from(summary?.querySelectorAll<HTMLElement>('[data-summary-plan]') ?? []);

  const showPlan = (planId: string | null) => {
    let shown = false;
    planPanels.forEach((panel) => {
      const match = panel.dataset.summaryPlan === planId;
      panel.hidden = !match;
      shown ||= match;
    });
    if (none) none.hidden = shown;
    if (summary && shown && !prefersReducedMotion) {
      // Restart the "updated" flash on every change; removing and re-adding in the same frame
      // does nothing, so the reflow in between is deliberate.
      summary.classList.remove('is-updated');
      void summary.offsetWidth;
      summary.classList.add('is-updated');
    }
  };

  radios.forEach((radio, index) => {
    radio.addEventListener('change', () => {
      if (!radio.checked) return;
      showPlan(radio.value);
      // Keyboard users move through the radios with the arrow keys; keep the chosen card in view.
      if (swiper && !swiper.isLocked) {
        const slide = swiper.slides[index];
        if (slide && !slide.classList.contains('swiper-slide-fully-visible')) swiper.slideTo(index);
      }
    });
  });

  // --- Promo code echo ------------------------------------------------------------------------
  const promoInput = form.querySelector<HTMLInputElement>('[data-join-promo]');
  const promoLine = summary?.querySelector<HTMLElement>('[data-summary-promo]') ?? null;
  const promoText = promoLine?.querySelector<HTMLElement>('[data-summary-promo-text]') ?? null;

  const syncPromo = () => {
    if (!promoInput || !promoLine || !promoText) return;
    const code = promoInput.value.trim().toUpperCase();
    promoLine.hidden = code.length === 0;
    if (code) promoText.textContent = (promoText.dataset.template ?? '{0}').replace('{0}', code);
  };
  promoInput?.addEventListener('input', syncPromo);
  syncPromo();

  // --- Step completion ------------------------------------------------------------------------
  // A step is done when every field the server will insist on has something in it. Required-ness
  // comes from the validation attributes the tag helpers already emit, so this never disagrees
  // with the model. Purely a visual tick — nothing is gated on it.
  const steps = Array.from(form.querySelectorAll<HTMLElement>('[data-step]'));

  const stepComplete = (step: HTMLElement): boolean => {
    const fields = Array.from(step.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>(
      'input, select, textarea',
    ));
    const radiosHere = fields.filter((f): f is HTMLInputElement => f instanceof HTMLInputElement && f.type === 'radio');
    if (radiosHere.length > 0 && !radiosHere.some((r) => r.checked)) return false;

    return fields.every((field) => {
      if (field instanceof HTMLInputElement && field.type === 'radio') return true;
      if (field instanceof HTMLInputElement && field.type === 'checkbox') return field.checked;
      if (field instanceof HTMLInputElement && field.type === 'hidden') return true;
      const required = field.hasAttribute('required') || field.hasAttribute('data-val-required');
      return !required || field.value.trim().length > 0;
    });
  };

  const syncSteps = () => {
    steps.forEach((step) => step.classList.toggle('is-complete', stepComplete(step)));
  };
  form.addEventListener('input', syncSteps);
  form.addEventListener('change', syncSteps);
  syncSteps();

  // --- Submit ---------------------------------------------------------------------------------
  const submit = form.querySelector<HTMLButtonElement>('[data-join-submit]');
  form.addEventListener('submit', (event) => {
    // jQuery Validate cancels the event when the form is invalid; that verdict is only final once
    // every handler has run, hence the deferral.
    window.setTimeout(() => {
      if (event.defaultPrevented || !submit) return;
      submit.classList.add('is-busy');
      submit.setAttribute('aria-busy', 'true');
      const label = submit.querySelector<HTMLElement>('.join__submit-label');
      if (label && submit.dataset.busyLabel) label.textContent = submit.dataset.busyLabel;
      // Disabled after the submission has started, so the click is never lost; the point is to
      // stop a second one while Stripe's page is on its way.
      submit.disabled = true;
    }, 0);
  });

  // Coming back from Stripe's own back button restores the page from the bfcache with the button
  // still busy.
  window.addEventListener('pageshow', () => {
    if (!submit) return;
    submit.disabled = false;
    submit.classList.remove('is-busy');
    submit.removeAttribute('aria-busy');
    const label = submit.querySelector<HTMLElement>('.join__submit-label');
    if (label && submit.dataset.idleLabel) label.textContent = submit.dataset.idleLabel;
  });
  if (submit) {
    const label = submit.querySelector<HTMLElement>('.join__submit-label');
    if (label) submit.dataset.idleLabel = label.textContent ?? '';
  }
}
