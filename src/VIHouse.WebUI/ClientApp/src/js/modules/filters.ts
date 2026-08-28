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

  async function apply(query: string, push: boolean): Promise<void> {
    inFlight?.abort();
    const controller = new AbortController();
    inFlight = controller;

    results!.setAttribute('aria-busy', 'true');

    try {
      const response = await fetch(`/experiences/results${query}`, {
        signal: controller.signal,
        headers: { 'X-Requested-With': 'fetch' },
      });
      if (!response.ok) throw new Error(`HTTP ${response.status}`);

      results!.innerHTML = await response.text();

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
   * The chips live inside the swapped-out region, so their handlers have to be re-attached after
   * every swap. Delegation from a stable ancestor would avoid that, but the chips sit outside
   * [data-experience-results] in the markup and rebinding two or three links is cheaper to read.
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

        // Keep the select in step with a chip-driven change, so a subsequent select change does not
        // silently drop the status the visitor just picked.
        const status = url.searchParams.get('status') ?? '';
        let hidden = form!.querySelector<HTMLInputElement>('input[name="status"]');
        if (!hidden) {
          hidden = document.createElement('input');
          hidden.type = 'hidden';
          hidden.name = 'status';
          form!.appendChild(hidden);
        }
        hidden.value = status;
      });
    });
  }

  // Back/forward has to re-render, or the address bar and the grid disagree.
  window.addEventListener('popstate', () => apply(window.location.search, false));

  bindChips();
}
