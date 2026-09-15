import Swiper from 'swiper';
import { Navigation, Pagination, A11y, Keyboard, Autoplay, EffectFade } from 'swiper/modules';
import 'swiper/css';
import 'swiper/css/navigation';
import 'swiper/css/pagination';
import 'swiper/css/effect-fade';

/**
 * Turns any `[data-swiper]` element into a carousel.
 *
 * The markup is authored so that it degrades to a plain row without JS — the Swiper classes only
 * take effect once Swiper itself initialises — so a failed bundle costs the arrows and the drag
 * behaviour, not the content.
 *
 * `data-swiper-per-view` sets the desktop slide count; smaller breakpoints step down from it, since
 * every carousel on the site shows the same kind of card and only differs in how many fit.
 *
 * Controls live in one of two places. A `[data-swiper-controls]` cluster inside the nearest
 * `[data-swiper-scope]` (the section header, so the arrows never sit on top of a photograph) is
 * preferred; failing that, Swiper's own `.swiper-button-*` / `.swiper-pagination` children of the
 * container (the gallery on the experience page). Either way the controls are hidden outright
 * while everything fits — Swiper's `watchOverflow` locks them, and a locked arrow beside four
 * cards that already fit reads as broken rather than idle.
 */
export function initCarousels(): void {
  const containers = Array.from(document.querySelectorAll<HTMLElement>('[data-swiper]'));
  if (containers.length === 0) return;

  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  containers.forEach((container) => {
    const perView = Number(container.dataset.swiperPerView ?? '3');
    const scope = container.closest<HTMLElement>('[data-swiper-scope]');
    const cluster = scope?.querySelector<HTMLElement>('[data-swiper-controls]') ?? null;

    const prevEl = cluster?.querySelector<HTMLElement>('[data-swiper-prev]')
      ?? container.querySelector<HTMLElement>('.swiper-button-prev');
    const nextEl = cluster?.querySelector<HTMLElement>('[data-swiper-next]')
      ?? container.querySelector<HTMLElement>('.swiper-button-next');
    const paginationEl = cluster?.querySelector<HTMLElement>('[data-swiper-pagination]')
      ?? container.querySelector<HTMLElement>('.swiper-pagination');

    const swiper = new Swiper(container, {
      modules: [Navigation, Pagination, A11y, Keyboard],
      slidesPerView: 1.15,
      spaceBetween: 16,
      speed: prefersReducedMotion ? 0 : 650,
      grabCursor: true,
      keyboard: { enabled: true, onlyInViewport: true },
      // Only paginate when there's more to see than fits — a "carousel" of two cards on a wide
      // screen showing dead arrows and a single dot looks broken rather than interactive.
      watchOverflow: true,
      watchSlidesProgress: true,
      breakpoints: {
        640: { slidesPerView: Math.min(2, perView), spaceBetween: 24 },
        1024: { slidesPerView: perView, spaceBetween: 28 },
      },
      navigation: { nextEl, prevEl },
      pagination: {
        el: paginationEl,
        // The header cluster carries a thin progress bar rather than dots: it says "you are a
        // third of the way along" without a row of bullets competing with the section title.
        type: cluster ? 'progressbar' : 'bullets',
        clickable: !cluster,
      },

      a11y: {
        // Read from data attributes rather than hardcoded — the bundle cannot see the .resx files,
        // and this is a four-language site.
        prevSlideMessage: container.dataset.swiperPrevLabel || 'Previous slide',
        nextSlideMessage: container.dataset.swiperNextLabel || 'Next slide',
      },
      on: {
        // Swiper toggles its own lock classes on the buttons; the cluster as a whole follows
        // suit so the progress bar disappears with them rather than sitting alone at 100%.
        init: (s) => { if (cluster) cluster.hidden = s.isLocked; markPeeking(s); },
        lock: () => { if (cluster) cluster.hidden = true; },
        unlock: () => { if (cluster) cluster.hidden = false; },
        // setTranslate fires as the move begins and transitionEnd once it has settled; a drag
        // produces only the former, an arrow press both.
        setTranslate: markPeeking,
        transitionEnd: markPeeking,
        resize: markPeeking,
      },
    });

    // The last card is partly cut off at the track's edge by design (it invites the swipe); a
    // click on it should bring it fully into view rather than start a navigation the reader
    // cannot yet see the target of.
    container.addEventListener('click', (event) => {
      const slide = (event.target as HTMLElement).closest<HTMLElement>('.swiper-slide');
      if (!slide || swiper.isLocked || !slide.classList.contains('is-peeking')) return;
      event.preventDefault();
      const index = Array.from(slide.parentElement?.children ?? []).indexOf(slide);
      const visible = swiper.slides.filter((el) => !el.classList.contains('is-peeking')).length;
      swiper.slideTo(index > swiper.activeIndex ? Math.max(0, index - Math.max(1, visible) + 1) : index);
    });
  });
}

/**
 * Marks the slides that are partly outside the track with `is-peeking`, which the stylesheet dims.
 * Swiper's own `swiper-slide-fully-visible` is computed from slide progress and misses the last
 * card in view by a sub-pixel more often than not, so the geometry is measured directly instead.
 * Nothing is marked while the carousel is locked — every card fits, so none is cut off.
 */
function markPeeking(swiper: Swiper): void {
  const box = swiper.el.getBoundingClientRect();
  swiper.slides.forEach((slide) => {
    if (swiper.isLocked) {
      slide.classList.remove('is-peeking');
      return;
    }
    const rect = slide.getBoundingClientRect();
    const peeking = rect.left < box.left - 1 || rect.right > box.right + 1;
    slide.classList.toggle('is-peeking', peeking);
  });
}

/**
 * The homepage hero carousel — `[data-hero-swiper]`, rendered by Views/Home/_Hero.cshtml.
 *
 * Separate from initCarousels because almost nothing about it is the same: one full-bleed slide at
 * a time rather than a row of cards, a cross-fade rather than a slide, autoplay, and its own
 * controls. Sharing one initialiser between the two would mean a config object that is mostly
 * branches.
 *
 * The attribute is only present when there is more than one slide, so a single-panel hero costs
 * nothing here.
 */
export function initHeroSlider(): void {
  const container = document.querySelector<HTMLElement>('[data-hero-swiper]');
  if (!container) return;

  // Autoplay moves the page under someone who did not ask it to. Anyone who has said they prefer
  // reduced motion gets the carousel with its controls and no automatic advance — the content is
  // all still reachable, it just waits to be asked.
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  const delay = Number(container.dataset.heroAutoplay ?? '7000');

  const swiper = new Swiper(container, {
    modules: [Navigation, Pagination, A11y, Keyboard, Autoplay, EffectFade],
    slidesPerView: 1,
    loop: true,
    effect: 'fade',
    // Without this the outgoing slide stays fully opaque through the transition and the two
    // photographs cross over as a bright flash.
    fadeEffect: { crossFade: true },
    speed: 700,
    keyboard: { enabled: true },
    autoplay: prefersReducedMotion || delay <= 0
      ? false
      : {
          delay,
          // A visitor who reaches for the arrows is reading, not watching — autoplay stops for
          // good at that point rather than yanking the slide away mid-sentence.
          disableOnInteraction: true,
          pauseOnMouseEnter: true,
        },
    navigation: {
      nextEl: container.querySelector<HTMLElement>('.home-hero__nav--next'),
      prevEl: container.querySelector<HTMLElement>('.home-hero__nav--prev'),
    },
    pagination: {
      el: container.querySelector<HTMLElement>('.home-hero__pagination'),
      clickable: true,
    },
    a11y: {
      prevSlideMessage: container.dataset.swiperPrevLabel || 'Previous slide',
      nextSlideMessage: container.dataset.swiperNextLabel || 'Next slide',
    },
  });

  // A backgrounded tab keeps firing timers, so a visitor who comes back after lunch would return
  // to a hero that had cycled a hundred times and, with loop on, a stack of cloned slides worth of
  // work done for nobody.
  document.addEventListener('visibilitychange', () => {
    if (!swiper.autoplay) return;
    if (document.hidden) swiper.autoplay.stop();
    else if (!prefersReducedMotion) swiper.autoplay.start();
  });
}
