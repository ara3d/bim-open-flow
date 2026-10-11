// Hovering a chip highlights its source and every node that uses it (TKT-138).
// A canvas pointer listener, like portHover.ts: the highlight spans several
// cards, so it cannot live in one gratify part's hover channel.

export interface ChipHoverDeps {
  /** The source whose chip is under a canvas-relative CSS-pixel point, or null. */
  sourceAt(x: number, y: number): string | null;
  /** The hovered source changed (null: none). */
  onHover(sourceId: string | null): void;
}

/** Installs the listeners on `canvas`; returns dispose. */
export function installChipHover(canvas: HTMLCanvasElement, deps: ChipHoverDeps): () => void {
  let current: string | null = null;
  const set = (sourceId: string | null) => {
    if (sourceId === current) return;
    current = sourceId;
    deps.onHover(sourceId);
  };
  const move = (event: PointerEvent) => {
    if (event.buttons !== 0) return set(null); // a pan or drag is not a hover
    const rect = canvas.getBoundingClientRect();
    set(deps.sourceAt(event.clientX - rect.left, event.clientY - rect.top));
  };
  const leave = () => set(null);
  canvas.addEventListener("pointermove", move);
  canvas.addEventListener("pointerleave", leave);
  return () => {
    canvas.removeEventListener("pointermove", move);
    canvas.removeEventListener("pointerleave", leave);
  };
}
