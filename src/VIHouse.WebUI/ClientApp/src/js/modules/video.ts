/**
 * Click-to-play video embeds in journal articles.
 *
 * The server renders `<div class="video-embed" data-video-id="…">` with a poster and a play button
 * (see ArticleHtml) and no player at all. This swaps in the iframe on the first click.
 *
 * Why not just render the iframe: an embedded YouTube player is around a megabyte of script that
 * runs on every article view, and it starts a conversation with Google before the reader has shown
 * any interest in the video. Deferring it costs one click and nothing else — and if this module
 * never loads, the poster is still a link-shaped thing that does nothing, so the button carries the
 * only behaviour worth guarding.
 */
export function initVideoEmbeds(): void {
  const embeds = Array.from(document.querySelectorAll<HTMLElement>('.video-embed[data-video-id]'));
  if (embeds.length === 0) return;

  embeds.forEach((embed) => {
    const button = embed.querySelector<HTMLButtonElement>('.video-embed__play');
    const videoId = embed.dataset.videoId;
    if (!button || !videoId) return;

    button.addEventListener('click', () => {
      // The id comes from the server, which parsed it out of a validated YouTube URL, but this is
      // the point where it becomes part of a URL in the browser — so it is checked again here
      // rather than trusted because of where it came from.
      if (!/^[\w-]{11}$/.test(videoId)) return;

      const iframe = document.createElement('iframe');
      iframe.className = 'video-embed__frame';
      iframe.src = `https://www.youtube-nocookie.com/embed/${videoId}?autoplay=1&rel=0`;
      iframe.title = button.getAttribute('aria-label') ?? 'Video';
      iframe.allow = 'accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture; fullscreen';
      iframe.allowFullscreen = true;
      iframe.setAttribute('referrerpolicy', 'strict-origin-when-cross-origin');
      iframe.setAttribute('loading', 'lazy');

      embed.replaceChildren(iframe);
      // The click that started playback also moved focus onto a button that no longer exists;
      // without this a keyboard user is returned to the top of the document.
      iframe.focus();
    });
  });
}
