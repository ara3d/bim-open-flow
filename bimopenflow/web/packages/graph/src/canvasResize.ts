// Node resize geometry (TKT-124): where the corner grip sits, and the size
// bounds a drag or a saved layout is held to. Pure and gratify-free, so it
// is testable headless and the view model can share it without a cycle
// (it imports nothing from viewModel.ts; callers pass the content size).

export interface Size {
  readonly w: number;
  readonly h: number;
}

/** Radius, in world units, around a card's bottom-right corner that starts a
 *  resize. Smaller than SOCKET_GRAB_RADIUS (12) so a press on the centre of
 *  a param-less card's last output socket, 12 px above the corner, still
 *  starts a wire. */
export const RESIZE_GRAB_RADIUS = 10;
/** Widest (and, for a note, tallest) a card may be dragged. A width past
 *  this stops reading as one node on a canvas at ordinary zoom. */
export const MAX_NODE_SIZE = 900;

/** True when `pointer` is within RESIZE_GRAB_RADIUS of `card`'s bottom-right
 *  corner. Both are in world units. */
export function resizeHandleHit(
  card: { readonly x: number; readonly y: number; readonly w: number; readonly h: number },
  pointer: { readonly x: number; readonly y: number },
): boolean {
  return Math.hypot(card.x + card.w - pointer.x, card.y + card.h - pointer.y) < RESIZE_GRAB_RADIUS;
}

/**
 * The size a card is drawn at, given the size its content needs (`content`,
 * the default the view model computes) and the size asked for (a drag, or a
 * saved layout entry). Width runs from the content width to MAX_NODE_SIZE.
 * Height does the same for a note; any other card's height follows its
 * ports and param slots, since extra height would only be empty space.
 * Holding a saved size to the content size also keeps a card whole when a
 * later edit (a longer note, a new param) makes its content grow.
 */
export function clampNodeSize(
  content: Size,
  want: { readonly w?: number; readonly h?: number },
  heightResizable: boolean,
): Size {
  const clamp = (value: number | undefined, min: number) =>
    Math.min(Math.max(value ?? min, min), Math.max(min, MAX_NODE_SIZE));
  return {
    w: clamp(want.w, content.w),
    h: heightResizable ? clamp(want.h, content.h) : content.h,
  };
}
