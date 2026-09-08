/**
 * The live countdown on an experience that has not opened yet.
 *
 * Progressive enhancement, the same shape as modules/filters.ts and modules/subnav.ts: the server
 * has already rendered the real opening date inside the <time> element, so with this bundle
 * missing the visitor still reads "Applications open 1 October 2026" — a correct, useful sentence.
 * This only rewrites that text into something that moves.
 *
 * The strings live in data attributes rather than in here, because the site runs in four languages
 * and a hardcoded "days" would be English on all of them.
 */

interface GateStrings {
  /** "Applications open in {0}" — {0} is the assembled duration. */
  inTemplate: string;
  /** Shown once the moment arrives and we are still on a page that says it has not. */
  imminent: string;
  day: string;
  days: string;
  hour: string;
  hours: string;
  minute: string;
  minutes: string;
}

const MINUTE = 60_000;
const HOUR = 60 * MINUTE;
const DAY = 24 * HOUR;

export function initExperienceGate(): void {
  const elements = document.querySelectorAll<HTMLTimeElement>('[data-gate-countdown]');
  if (elements.length === 0) return;

  // Reduced motion gets the same information without the per-second movement: rendered once, at
  // day granularity, and never updated. Matching the stylesheet, which cancels the ring and the
  // shimmer under the same query.
  const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  elements.forEach((el) => {
    const target = Date.parse(el.dateTime);
    // An unparseable or absent datetime leaves the server's own text exactly as it is. The view
    // only emits this element when the date is set and in the future, so this is defence against
    // a malformed value rather than an expected path.
    if (Number.isNaN(target)) return;

    const strings = readStrings(el);
    if (!strings) return;

    const render = (): boolean => {
      const remaining = target - Date.now();

      if (remaining <= 0) {
        // Never count downwards past zero. The status is admin-controlled and the server may still
        // be refusing applications, so this says "any moment now" rather than claiming it is open —
        // a page that contradicts the button underneath it is worse than one that is merely vague.
        el.textContent = strings.imminent;
        return false;
      }

      el.textContent = strings.inTemplate.replace('{0}', describe(remaining, strings, reduced));
      return true;
    };

    if (!render() || reduced) return;

    // Ticking every second below the hour and every minute above it: past an hour out, a seconds
    // counter is noise, and a timer that fires 3,600 times to change nothing is worse than noise.
    const period = target - Date.now() < HOUR ? 1_000 : MINUTE;
    const timer = window.setInterval(() => {
      if (!render()) window.clearInterval(timer);
    }, period);
  });
}

/**
 * The plural forms, read off the element. Any missing attribute aborts the whole enhancement for
 * that element rather than letting an "undefined" reach the page.
 */
function readStrings(el: HTMLElement): GateStrings | null {
  const d = el.dataset;
  const required = [
    d.gateIn, d.gateImminent,
    d.gateDay, d.gateDays, d.gateHour, d.gateHours, d.gateMinute, d.gateMinutes,
  ];
  if (required.some((v) => v === undefined)) return null;

  return {
    inTemplate: d.gateIn!,
    imminent: d.gateImminent!,
    day: d.gateDay!,
    days: d.gateDays!,
    hour: d.gateHour!,
    hours: d.gateHours!,
    minute: d.gateMinute!,
    minutes: d.gateMinutes!,
  };
}

/**
 * Two units at most, largest first: "12 days, 4 hours" rather than "12 days, 4 hours, 11 minutes,
 * 3 seconds". The second unit is dropped when it is zero, so "3 days" never renders as
 * "3 days, 0 hours".
 */
function describe(ms: number, s: GateStrings, coarse: boolean): string {
  const days = Math.floor(ms / DAY);
  const hours = Math.floor((ms % DAY) / HOUR);
  const minutes = Math.floor((ms % HOUR) / MINUTE);

  const plural = (n: number, one: string, many: string) => `${n} ${n === 1 ? one : many}`;

  if (coarse) {
    // Under reduced motion nothing will update this, so it rounds up rather than down: telling
    // someone "0 days" for something six hours away and then never correcting it would be wrong.
    if (days > 0) return plural(days, s.day, s.days);
    if (hours > 0) return plural(hours, s.hour, s.hours);
    return plural(Math.max(1, minutes), s.minute, s.minutes);
  }

  const parts: string[] = [];
  if (days > 0) {
    parts.push(plural(days, s.day, s.days));
    if (hours > 0) parts.push(plural(hours, s.hour, s.hours));
  } else if (hours > 0) {
    parts.push(plural(hours, s.hour, s.hours));
    if (minutes > 0) parts.push(plural(minutes, s.minute, s.minutes));
  } else {
    parts.push(plural(Math.max(1, minutes), s.minute, s.minutes));
  }

  return parts.join(', ');
}
