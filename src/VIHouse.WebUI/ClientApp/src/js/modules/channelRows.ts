/**
 * The influencer channels table (Views/Shared/_InfluencerProfileFields.cshtml) is rendered with a
 * few spare rows, because empty rows are ignored on save and no script should be needed to add one.
 * With script, the filled rows and the first empty one are shown, and "Add a channel" reveals the
 * next spare row — one row at a time instead of a wall of empty inputs.
 */
export function initChannelRows(): void {
  document.querySelectorAll<HTMLElement>('[data-channel-rows]').forEach((table) => {
    const rows = [...table.querySelectorAll<HTMLElement>('[data-channel-row]')];
    const add = table.querySelector<HTMLButtonElement>('[data-channel-add]');
    if (!add || rows.length === 0) return;

    const isEmpty = (row: HTMLElement): boolean =>
      [...row.querySelectorAll<HTMLInputElement>('input')].every((input) => input.value.trim() === '');

    let shownEmpty = false;
    rows.forEach((row) => {
      if (!isEmpty(row)) return;
      if (shownEmpty) row.hidden = true;
      shownEmpty = true;
    });

    const sync = (): void => { add.hidden = !rows.some((row) => row.hidden); };
    add.addEventListener('click', () => {
      const next = rows.find((row) => row.hidden);
      if (!next) return;
      next.hidden = false;
      next.querySelector<HTMLInputElement>('input[type="url"]')?.focus();
      sync();
    });
    sync();
  });
}
