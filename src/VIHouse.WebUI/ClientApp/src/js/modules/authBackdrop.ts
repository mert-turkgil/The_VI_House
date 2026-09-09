/**
 * Reveals the sign-in backdrop video, but only once it is genuinely playing.
 *
 * The video ships at `opacity: 0` and the photograph underneath is what everyone sees first. That
 * ordering is deliberate: `autoplay` is a request, not a guarantee. Safari on low-power mode, iOS
 * before a user gesture, Data Saver in Chrome and every browser that cannot decode the file all
 * refuse it silently, and a video element that never paints is a black rectangle, not a fallback.
 * Fading in on the first `playing` event means the photograph stays put in every one of those cases
 * and the loop is a bonus rather than a dependency.
 *
 * The element is only rendered when a video file actually exists on disk (see
 * Areas/Identity/Pages/Account/_Layout.cshtml), so on a checkout with no video this exits at the
 * first line.
 */
export function initAuthBackdrop(): void {
  const video = document.querySelector<HTMLVideoElement>('[data-auth-backdrop]');
  if (!video) return;

  // Reduced motion is a request not to animate, and a looping cityscape is exactly the kind of
  // continuous background movement the setting exists to stop. The video is not merely left paused
  // — `preload="none"` means nothing has been fetched yet, so returning here also saves the
  // download entirely. The still photograph is the whole design for these visitors.
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    video.removeAttribute('autoplay');
    video.pause();
    return;
  }

  video.addEventListener('playing', () => video.classList.add('is-playing'), { once: true });

  // The markup carries `autoplay`, which handles this on its own nearly everywhere. This call is
  // for the browsers that ignore the attribute but honour a scripted play() — and its rejection is
  // swallowed, because a refused autoplay is the expected outcome, not an error worth reporting.
  void video.play().catch(() => {
    /* Autoplay declined; the photograph is already showing and remains the backdrop. */
  });
}
