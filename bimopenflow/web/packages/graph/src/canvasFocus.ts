// A press on the canvas takes keyboard focus (TKT-103). Gratify's pointerdown
// does not move focus, and it reads keys from window, skipping any key whose
// target is an editable element. Without this, a text box in the host's chrome
// (the studio's node filter, a Steps field) kept focus after a click on a
// node, so Delete and Ctrl+Z edited that text instead of the graph.

/**
 * Makes the canvas focusable (tabindex -1 unless the host set one) and focuses
 * it on every pointerdown. Install before gratify's mount so the focus moves
 * before gratify handles the press. preventScroll keeps a canvas embedded in a
 * long page, such as a notebook cell, from scrolling the page on click.
 * Returns the function that removes the listener.
 */
export function installCanvasFocus(canvas: HTMLCanvasElement): () => void {
  if (!canvas.hasAttribute("tabindex")) canvas.tabIndex = -1;
  const onPointerDown = () => canvas.focus({ preventScroll: true });
  canvas.addEventListener("pointerdown", onPointerDown);
  return () => canvas.removeEventListener("pointerdown", onPointerDown);
}
