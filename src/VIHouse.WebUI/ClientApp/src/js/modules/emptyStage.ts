/**
 * The soft light on the empty-stage panel (Views/Shared/_EmptyStage.cshtml) follows the pointer.
 * It only writes two CSS variables, once per animation frame; the gradient itself is CSS. Skipped
 * entirely for reduced motion and for touch-only devices, where there is no pointer to follow.
 */
export function initEmptyStage(): void {
  const stages = document.querySelectorAll<HTMLElement>('[data-empty-stage]');
  if (stages.length === 0) return;
  if (window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;
  if (!window.matchMedia('(hover: hover)').matches) return;

  stages.forEach((stage) => {
    let frame = 0;
    let x = 0;
    let y = 0;

    stage.addEventListener('pointermove', (event) => {
      const rect = stage.getBoundingClientRect();
      x = ((event.clientX - rect.left) / rect.width) * 100;
      y = ((event.clientY - rect.top) / rect.height) * 100;
      if (frame) return;
      frame = requestAnimationFrame(() => {
        stage.style.setProperty('--stage-x', `${x.toFixed(1)}%`);
        stage.style.setProperty('--stage-y', `${y.toFixed(1)}%`);
        frame = 0;
      });
    });

    stage.addEventListener('pointerleave', () => {
      stage.style.removeProperty('--stage-x');
      stage.style.removeProperty('--stage-y');
    });
  });
}
