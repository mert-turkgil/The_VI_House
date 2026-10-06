/**
 * Upgrades the Experiences listing filters from full page loads to fetch-and-swap.
 *
 * This is an enhancement, not the mechanism. The city control is a real <form method="get"> and the
 * status chips are real <a href> links, so the page filters correctly with scripting off — and if
 * anything here throws, the browser's own navigation still works.
 *
 * The server returns the same Razor partial the full page renders (ExperiencesController.Results),
 * so there is exactly one definition of what a card looks like. Returning JSON and rebuilding the
 * markup here would mean two renderers to keep in step, which is how the enhanced and unenhanced
 * views of a page start telling visitors different things.
 */

const DEBOUNCE_MS = 250;

// How long the new cards' entrance runs (the last of six staggered cells, plus its own animation).
// The class is removed afterwards so a later hover is not fighting a finished animation.
const ENTER_MS = 1000;

export function initExperienceFilters(): void {
  const form = document.querySelector<HTMLFormElement>('[data-experience-filters]');
  const results = document.querySelector<HTMLElement>('[data-experience-results]');
  if (!form || !results) return;

  // Only meaningful once we know we can enhance: without JS the submit button is the only way to
  // apply the select, so it must stay in the markup and be removed here rather than the reverse.
  form.querySelectorAll('button[type="submit"]').forEach((button) => button.remove());

  // One in-flight request at a time. Without this, a fast series of changes can resolve out of
  // order and leave the grid showing the results of a filter the visitor already moved past.
  let inFlight: AbortController | null = null;
  let debounce: number | undefined;
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /**
   * The gold pill behind each chip group's active chip. One element per group, moved with a
   * transform, so changing the filter slides it from the old chip to the new one instead of one
   * chip switching off and another switching on. The group gets .has-pill, which makes the chips'
   * own active background transparent; without this script the chips keep their own background.
   */
  const groups = Array.from(document.querySelectorAll<HTMLElement>('.experience-filters__chips'));
  groups.forEach((group) => {
    const pill = document.createElement('span');
    pill.className = 'experience-filters__pill';
    pill.setAttribute('aria-hidden', 'true');
    group.prepend(pill);
    group.classList.add('has-pill');
  });

  function placePills(): void {
    groups.forEach((group) => {
      const pill = group.querySelector<HTMLElement>('.experience-filters__pill');
      const active = group.querySelector<HTMLElement>('.experience-filters__chip--active');
      if (!pill) return;
      if (!active) {
        pill.style.opacity = '0';
        return;
      }
      pill.style.opacity = '1';
      pill.style.width = `${active.offsetWidth}px`;
      pill.style.height = `${active.offsetHeight}px`;
      pill.style.transform = `translate(${active.offsetLeft}px, ${active.offsetTop}px)`;

      // On a phone the rail scrolls: bring the newly active chip into view within the rail only,
      // never by scrolling the page.
      const rail = group.closest<HTMLElement>('.experience-filters__chips-scroll');
      if (rail && rail.scrollWidth > rail.clientWidth) {
        const target = active.offsetLeft - (rail.clientWidth - active.offsetWidth) / 2;
        rail.scrollTo({ left: Math.max(0, target), behavior: prefersReducedMotion ? 'auto' : 'smooth' });
      }
    });
  }

  // First placement without a slide (it would otherwise fly in from the corner on page load).
  groups.forEach((group) => group.classList.add('is-placing'));
  placePills();
  requestAnimationFrame(() => groups.forEach((group) => group.classList.remove('is-placing')));
  window.addEventListener('resize', placePills);
  document.fonts?.ready.then(placePills);

  /**
   * Makes the panel agree with the filter that was just applied — the city select's value, which
   * status chip (if any) is highlighted, and whether the Clear link has anything to clear.
   *
   * Takes the query string rather than re-reading form state, because it has to handle three
   * different callers with three different sources of truth: a city change (the form), a chip
   * click (the chip's own href), and browser back/forward (window.location.search). All three
   * already produce the same "?city=x&status=y" shape, so one function can read any of them.
   */
  function syncFilterUI(query: string): void {
    const params = new URLSearchParams(query);
    const status = params.get('status') ?? '';
    const city = params.get('city') ?? '';
    const topic = params.get('topic') ?? '';

    const citySelect = form!.querySelector<HTMLSelectElement>('#filter-city');
    if (citySelect && citySelect.value !== city) citySelect.value = city;

    // Status and trending chips share this class, and every chip's href carries *both* params —
    // a trending chip keeps whatever status is active, and vice versa. Comparing every chip
    // against the same "status" value regardless of which filter it actually represents is what
    // made every trending chip light up together: none of them differ on status when no status is
    // selected, so they all read as "active". data-filter-field says which param is this chip's own.
    document.querySelectorAll<HTMLAnchorElement>('.experience-filters__chip').forEach((chip) => {
      const field = chip.dataset.filterField === 'topic' ? topic : status;
      const isActive = (chip.dataset.filterValue ?? '') === field;
      chip.classList.toggle('experience-filters__chip--active', isActive);
      if (isActive) chip.setAttribute('aria-current', 'true');
      else chip.removeAttribute('aria-current');
    });

    const clear = document.querySelector<HTMLAnchorElement>('[data-experience-clear]');
    if (clear) clear.hidden = !status && !city && !topic;

    placePills();
  }

  async function apply(query: string, push: boolean): Promise<void> {
    // Updated first, before the request even lands: the chips, the city select and the Clear link
    // sit outside [data-experience-results], so nothing about swapping the grid's innerHTML ever
    // touched them. The grid was filtering correctly the whole time — the panel just never said so,
    // which is indistinguishable from "the filter didn't do anything" to whoever clicked it.
    syncFilterUI(query);

    inFlight?.abort();
    const controller = new AbortController();
    inFlight = controller;

    results!.setAttribute('aria-busy', 'true');
    // The current cards dim while the new ones load, so the click visibly did something.
    results!.classList.add('is-loading');

    try {
      const response = await fetch(`/experiences/results${query}`, {
        signal: controller.signal,
        headers: { 'X-Requested-With': 'fetch' },
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      results!.innerHTML = await response.text();

      // The swapped-in cells were never seen by the scroll-reveal observer, so they get their own
      // staggered entrance (CSS, reading each cell's --i). Visible without it either way.
      if (!prefersReducedMotion) {
        results!.classList.add('is-entering');
        window.setTimeout(() => results!.classList.remove('is-entering'), ENTER_MS);
      }

      if (push) history.pushState({}, '', `/experiences${query}`);
      bindChips();
    } catch (error) {
      if ((error as Error).name === 'AbortError') return;
      // Fall back to a real navigation. Leaving the visitor on a stale grid with no explanation is
      // the one outcome worse than a page load.
      window.location.href = `/experiences${query}`;
    } finally {
      if (inFlight === controller) {
        results!.removeAttribute('aria-busy');
        results!.classList.remove('is-loading');
        inFlight = null;
      }
    }
  }

  function queryFromForm(): string {
    const params = new URLSearchParams(new FormData(form!) as unknown as Record<string, string>);
    // Empty values would otherwise show up as "?city=&status=" in the address bar.
    for (const [key, value] of [...params.entries()]) {
      if (!value) params.delete(key);
    }
    const query = params.toString();
    return query ? `?${query}` : '';
  }

  form.addEventListener('change', () => {
    window.clearTimeout(debounce);
    debounce = window.setTimeout(() => apply(queryFromForm(), true), DEBOUNCE_MS);
  });

  form.addEventListener('submit', (event) => {
    event.preventDefault();
    apply(queryFromForm(), true);
  });

  /**
   * Binds the chips' click handlers, once each — the dataset guard is what makes calling this
   * repeatedly harmless. The chips sit outside [data-experience-results] and never get replaced,
   * so a single call at init would in fact be enough; this is called again after every apply()
   * purely as a defensive no-op, in case a future change ever does re-render them.
   */
  function bindChips(): void {
    document.querySelectorAll<HTMLAnchorElement>('.experience-filters__chip').forEach((chip) => {
      if (chip.dataset.bound) return;
      chip.dataset.bound = 'true';

      chip.addEventListener('click', (event) => {
        // Let modified clicks (new tab, new window) behave normally.
        if (event.metaKey || event.ctrlKey || event.shiftKey || event.button !== 0) return;

        event.preventDefault();
        const url = new URL(chip.href, window.location.origin);
        apply(url.search, true);

        // Keep the form in step with a chip-driven change, so a subsequent city select change does
        // not silently drop the status or topic the visitor just picked — both hidden inputs, since
        // either kind of chip carries both params through.
        for (const name of ['status', 'topic']) {
          const value = url.searchParams.get(name) ?? '';
          let hidden = form!.querySelector<HTMLInputElement>(`input[name="${name}"]`);
          if (!hidden) {
            hidden = document.createElement('input');
            hidden.type = 'hidden';
            hidden.name = name;
            form!.appendChild(hidden);
          }
          hidden.value = value;
        }
      });
    });
  }

  // Back/forward has to re-render, or the address bar and the grid disagree.
  window.addEventListener('popstate', () => apply(window.location.search, false));

  bindChips();
}
