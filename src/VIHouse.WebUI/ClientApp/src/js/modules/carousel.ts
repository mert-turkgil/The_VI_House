import Swiper from 'swiper';
import { Navigation, Pagination, A11y, Keyboard, Autoplay, EffectFade } from 'swiper/modules';
import 'swiper/css';
import 'swiper/css/navigation';
import 'swiper/css/pagination';
import 'swiper/css/effect-fade';

/**
 * Turns any `[data-swiper]` element into a carousel.
 *
 * The markup is authored so that it degrades to a plain horizontal row without JS — the Swiper
 * classes only take effect once Swiper itself initialises — so a failed bundle costs the arrows and
 * the drag behaviour, not the content.
 *
 * `data-swiper-per-view` sets the desktop slide count; smaller breakpoints step down from it, since
 * every carousel on the site shows the same kind of card and only differs in how many fit.
 */
export function initCarousels(): void {
  const containers = Array.from(document.querySelectorAll<HTMLElement>('[data-swiper]'));
  if (containers.length === 0) return;

  containers.forEach((container) => {
    const perView = Number(container.dataset.swiperPerView ?? '3');

    new Swiper(container, {
      modules: [Navigation, Pagination, A11y, Keyboard],
      slidesPerView: 1.1,
      spaceBetween: 20,
      grabCursor: true,
      keyboard: { enabled: true },
      // Only paginate when there's more to see than fits — a "carousel" of two cards on a wide
      // screen showing dead arrows and a single dot looks broken rather than interactive.
      watchOverflow: true,
      breakpoints: {
        640: { slidesPerView: Math.min(2, perView), spaceBetween: 24 },
        1024: { slidesPerView: perView, spaceBetween: 28 },
      },
      navigation: {
        nextEl: container.querySelector<HTMLElement>('.swiper-button-next'),
        prevEl: container.querySelector<HTMLElement>('.swiper-button-prev'),
      },
      pagination: {
        el: container.querySelector<HTMLElement>('.swiper-pagination'),
        clickable: true,
      },

      a11y: {
        // Read from data attributes rather than hardcoded, the same way qr.ts takes its localized
        // strings — the bundle cannot see the .resx files, and this is a four-language site.
        prevSlideMessage: container.dataset.swiperPrevLabel || 'Previous slide',
        nextSlideMessage: container.dataset.swiperNextLabel || 'Next slide',
      },
    });
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
