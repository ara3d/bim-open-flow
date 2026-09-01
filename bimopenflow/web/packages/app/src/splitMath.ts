// Pure math for the shell's column splitters. sign is +1 when dragging right
// grows the column (left sidebar) and -1 when dragging left grows it (right
// pane area).

export interface SplitSpec {
  min: number;
  max: number;
  sign: 1 | -1;
}

export const clampWidth = (width: number, min: number, max: number): number =>
  Math.min(max, Math.max(min, width));

/** The clamped column width for a pointer at x, given the drag start. */
export const dragWidth = (
  startWidth: number,
  startX: number,
  x: number,
  spec: SplitSpec,
): number =>
  clampWidth(startWidth + spec.sign * (x - startX), spec.min, spec.max);

/** Where the ghost line sits for a (possibly clamped) width. Equals the
 *  pointer x while the width is unclamped; pins at the limit otherwise. */
export const ghostX = (
  startX: number,
  startWidth: number,
  width: number,
  sign: 1 | -1,
): number => startX + sign * (width - startWidth);
