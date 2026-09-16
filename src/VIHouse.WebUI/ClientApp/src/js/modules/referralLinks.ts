/**
 * The Site / Experiences / Sessions tabs on the referral-links panel (Views/Shared/_ReferralLinks).
 * Pure show/hide on `[data-referral-tab]` → `[data-referral-panel]`; without JS every panel is
 * still in the DOM, the first is visible and the others carry `hidden`, so the site link — the one
 * that matters most — is never out of reach.
 */
export function initReferralLinks(): void {
  document.querySelectorAll<HTMLElement>('[data-referral-links]').forEach((root) => {
    const tabs = Array.from(root.querySelectorAll<HTMLButtonElement>('[data-referral-tab]'));
    const panels = Array.from(root.querySelectorAll<HTMLElement>('[data-referral-panel]'));
    if (tabs.length === 0) return;

    const show = (name: string) => {
      tabs.forEach((tab) => {
        const active = tab.dataset.referralTab === name;
        tab.classList.toggle('is-active', active);
        tab.setAttribute('aria-selected', active ? 'true' : 'false');
        tab.tabIndex = active ? 0 : -1;
      });
      panels.forEach((panel) => {
        panel.hidden = panel.dataset.referralPanel !== name;
      });
    };

    tabs.forEach((tab, index) => {
      tab.addEventListener('click', () => show(tab.dataset.referralTab ?? 'site'));
      tab.addEventListener('keydown', (event) => {
        if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return;
        event.preventDefault();
        const next = tabs[(index + (event.key === 'ArrowRight' ? 1 : tabs.length - 1)) % tabs.length];
        show(next.dataset.referralTab ?? 'site');
        next.focus();
      });
    });

    // Deep link: #referral-experiences opens that tab (the admin's "Send link" email points here).
    const fromHash = window.location.hash.replace('#referral-', '');
    if (fromHash && tabs.some((t) => t.dataset.referralTab === fromHash)) show(fromHash);
  });
}
