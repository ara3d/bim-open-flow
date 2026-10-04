export const HOVER_DELAY_MS = 300;
export const CLICK_SLOP_PX = 4;

export interface PortHoverDeps {
  /** Endpoint under a canvas-relative CSS-pixel point, or null. */
  targetAt(x: number, y: number): string | null;
  hover(endpoint: string | null): void;
  pin(endpoint: string | null): void;
}

/** Installs pointer and key listeners on the canvas; returns dispose. */
export function installPortHover(canvas: HTMLCanvasElement, deps: PortHoverDeps): () => void {
  const document = canvas.ownerDocument;

  let pendingTarget: string | null = null;
  let lastSent: string | null = null;
  let timer: ReturnType<typeof setTimeout> | null = null;

  // undefined: no press in progress. Set on pointerdown, cleared on pointerup.
  let downTarget: string | null | undefined;
  let downX = 0;
  let downY = 0;

  const clearTimer = () => {
    if (timer !== null) {
      clearTimeout(timer);
      timer = null;
    }
  };

  const setTarget = (target: string | null) => {
    if (target === pendingTarget) return;
    clearTimer();
    pendingTarget = target;
    if (target === null) {
      if (lastSent !== null) {
        lastSent = null;
        deps.hover(null);
      }
      return;
    }
    timer = setTimeout(() => {
      timer = null;
      lastSent = target;
      deps.hover(target);
    }, HOVER_DELAY_MS);
  };

  const canvasPoint = (event: PointerEvent) => {
    const rect = canvas.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  };

  const pointerMove = (event: PointerEvent) => {
    if (event.buttons !== 0) return; // a held button means a pan, node drag, or wire drag.
    const { x, y } = canvasPoint(event);
    setTarget(deps.targetAt(x, y));
  };

  const pointerLeave = () => {
    setTarget(null);
  };

  const pointerDown = (event: PointerEvent) => {
    clearTimer();
    const { x, y } = canvasPoint(event);
    downTarget = deps.targetAt(x, y);
    downX = x;
    downY = y;
    if (downTarget === null) deps.pin(null);
  };

  const pointerUp = (event: PointerEvent) => {
    if (downTarget === undefined) return;
    if (downTarget !== null) {
      const { x, y } = canvasPoint(event);
      if (Math.hypot(x - downX, y - downY) <= CLICK_SLOP_PX) deps.pin(downTarget);
    }
    downTarget = undefined;
  };

  const keyDown = (event: KeyboardEvent) => {
    if (event.key === "Escape") deps.pin(null);
  };

  canvas.addEventListener("pointermove", pointerMove);
  canvas.addEventListener("pointerleave", pointerLeave);
  canvas.addEventListener("pointerdown", pointerDown);
  canvas.addEventListener("pointerup", pointerUp);
  document.addEventListener("keydown", keyDown, true);

  return () => {
    clearTimer();
    canvas.removeEventListener("pointermove", pointerMove);
    canvas.removeEventListener("pointerleave", pointerLeave);
    canvas.removeEventListener("pointerdown", pointerDown);
    canvas.removeEventListener("pointerup", pointerUp);
    document.removeEventListener("keydown", keyDown, true);
  };
}
