/**
 * "Ask the House" on the About page (Views/About/_HouseGuide.cshtml): plays the localized JSON
 * island as a short conversation — topic, question, answer — with a three-step progress bar. Every
 * answer ends in links (FAQ, a page) and, for a person, a mailto to the right address. Nothing is
 * sent anywhere; the chat only reveals what the page already contains.
 *
 * Also exports initFaqDeepLinks: /faq#q4 opens and scrolls to that answer, which is what the
 * guide's FAQ links rely on.
 */

interface GuideLink { label: string; href: string | null }
interface GuideQuestion { label: string; answer: string; links: GuideLink[] }
interface GuideTopic { id: string; label: string; intro: string; mail: 'concierge' | 'support'; questions: GuideQuestion[] }
interface GuideMail { address: string; subject: string }
interface GuideData {
  greeting: string;
  restart: string;
  stillNeed: string;
  typing: string;
  mail: Record<'concierge' | 'support', GuideMail>;
  topics: GuideTopic[];
}

const ICONS: Record<string, string> = {
  join: '<path d="M12 3l8 4v5c0 4.5-3.4 8.2-8 9-4.6-.8-8-4.5-8-9V7z"/><path d="M9 12l2 2 4-4"/>',
  membership: '<circle cx="9" cy="8" r="3.2"/><path d="M3 20a6 6 0 0 1 12 0M16 4.8a3.2 3.2 0 0 1 0 6.3M18 14.2a6 6 0 0 1 3 5.8"/>',
  experiences: '<path d="M12 21s-7-6.2-7-11a7 7 0 0 1 14 0c0 4.8-7 11-7 11z"/><circle cx="12" cy="10" r="2.5"/>',
  booking: '<rect x="3" y="6" width="18" height="12" rx="2"/><path d="M3 10h18M7 15h4"/>',
  other: '<path d="M4 5h16v11H8l-4 4z"/><path d="M8 9.5h8M8 12.5h5"/>',
  question: '<circle cx="12" cy="12" r="9"/><path d="M9.5 9.5a2.5 2.5 0 1 1 3.5 2.3c-.6.3-1 .9-1 1.6V14M12 17h.01"/>',
};

const reduceMotion = (): boolean => window.matchMedia('(prefers-reduced-motion: reduce)').matches;

function icon(id: string): string {
  return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${ICONS[id] ?? ICONS.question}</svg>`;
}

function el<K extends keyof HTMLElementTagNameMap>(tag: K, className: string, text?: string): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

export function initHouseGuide(): void {
  const card = document.querySelector<HTMLElement>('[data-house-guide]');
  const body = card?.querySelector<HTMLElement>('[data-house-guide-body]');
  const island = card?.querySelector<HTMLScriptElement>('[data-house-guide-data]');
  if (!card || !body || !island) return;

  const data = JSON.parse(island.textContent ?? '{}') as GuideData;
  const steps = Array.from(card.querySelectorAll<HTMLElement>('.house-guide__progress [data-step]'));
  let started = false;

  const progress = (step: number): void => {
    steps.forEach((s, i) => {
      s.classList.toggle('is-done', i < step);
      s.classList.toggle('is-active', i === step);
    });
  };

  const scroll = (): void => {
    body.scrollTo({ top: body.scrollHeight, behavior: reduceMotion() ? 'auto' : 'smooth' });
  };

  // A short "typing" pause before each bot line; none at all for reduced motion.
  const say = (text: string, after: () => void = () => undefined): void => {
    const typing = el('div', 'house-guide__typing');
    typing.setAttribute('aria-label', data.typing);
    typing.innerHTML = '<span></span><span></span><span></span>';
    body.appendChild(typing);
    scroll();
    window.setTimeout(() => {
      typing.remove();
      body.appendChild(el('p', 'house-guide__bubble', text));
      after();
      scroll();
    }, reduceMotion() ? 0 : 650);
  };

  const echo = (text: string): void => {
    body.appendChild(el('p', 'house-guide__bubble house-guide__bubble--me', text));
    scroll();
  };

  const options = (items: { label: string; icon: string; pick: () => void }[]): void => {
    const list = el('div', 'house-guide__options');
    items.forEach((item, index) => {
      const button = el('button', 'house-guide__option');
      button.type = 'button';
      button.style.setProperty('--i', String(index));
      button.innerHTML = `<span class="house-guide__option-icon">${icon(item.icon)}</span>`;
      button.appendChild(el('span', 'house-guide__option-label', item.label));
      button.addEventListener('click', () => {
        // The choice becomes the visitor's own bubble (echo), so the list itself goes — the
        // conversation stays short and the newest answer is always in view.
        list.remove();
        item.pick();
      });
      list.appendChild(button);
    });
    body.appendChild(list);
    scroll();
  };

  const answer = (topic: GuideTopic, question: GuideQuestion): void => {
    progress(2);
    say(question.answer, () => {
      const actions = el('div', 'house-guide__actions');
      question.links.filter((l) => l.href).forEach((link) => {
        const a = el('a', 'house-guide__link', link.label);
        a.href = link.href as string;
        actions.appendChild(a);
      });

      // The right person for this topic, with the subject already written.
      const mail = data.mail[topic.mail];
      const person = el('a', 'house-guide__mail');
      person.href = `mailto:${mail.address}?subject=${encodeURIComponent(mail.subject)}`;
      person.appendChild(el('span', 'house-guide__mail-label', data.stillNeed));
      person.appendChild(el('span', 'house-guide__mail-address', mail.address));
      actions.appendChild(person);

      const again = el('button', 'house-guide__restart', data.restart);
      again.type = 'button';
      again.addEventListener('click', start);
      actions.appendChild(again);

      body.appendChild(actions);
    });
  };

  const topicPicked = (topic: GuideTopic): void => {
    echo(topic.label);
    progress(1);
    say(topic.intro, () => options(topic.questions.map((q) => ({
      label: q.label,
      icon: 'question',
      pick: () => { echo(q.label); answer(topic, q); },
    }))));
  };

  function start(): void {
    body.innerHTML = '';
    progress(0);
    say(data.greeting, () => options(data.topics.map((t) => ({ label: t.label, icon: t.id, pick: () => topicPicked(t) }))));
  }

  // Starts when it comes into view, so the greeting is actually seen being "typed".
  const begin = (): void => {
    if (started) return;
    started = true;
    start();
  };

  if ('IntersectionObserver' in window) {
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((e) => e.isIntersecting)) {
        observer.disconnect();
        begin();
      }
    }, { threshold: 0.3 });
    observer.observe(card);
  } else {
    begin();
  }
}

/** /faq#q4 opens answer 4 and brings it into view; so do in-page links to #qN. */
export function initFaqDeepLinks(): void {
  const list = document.querySelector('.faq-list');
  if (!list) return;

  const open = (): void => {
    const match = /^#q(\d+)$/.exec(location.hash);
    if (!match) return;
    const item = document.getElementById(`q${match[1]}`);
    if (item instanceof HTMLDetailsElement) {
      item.open = true;
      item.scrollIntoView({ block: 'center', behavior: reduceMotion() ? 'auto' : 'smooth' });
    }
  };

  open();
  window.addEventListener('hashchange', open);
}
