import Swiper from 'swiper';
import { Navigation, Pagination, A11y, Keyboard } from 'swiper/modules';
import 'swiper/css';
import 'swiper/css/navigation';
import 'swiper/css/pagination';

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
        prevSlideMessage: 'Previous experience',
        nextSlideMessage: 'Next experience',
      },
    });
  });
}
