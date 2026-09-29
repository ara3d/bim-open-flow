// Node resize geometry (TKT-124): where the corner grip sits. The size
// bounds a drag is held to live in nodeSize.ts. Pure and gratify-free.

/** Radius, in world units, around a card's bottom-right corner that starts a
 *  resize. Smaller than SOCKET_GRAB_RADIUS (12) so a press on the centre of
 *  a param-less card's last output socket, 12 px above the corner, still
 *  starts a wire. */
export const RESIZE_GRAB_RADIUS = 10;

/** True when `pointer` is within RESIZE_GRAB_RADIUS of `card`'s bottom-right
 *  corner. Both are in world units. */
export function resizeHandleHit(
  card: { readonly x: number; readonly y: number; readonly w: number; readonly h: number },
  pointer: { readonly x: number; readonly y: number },
): boolean {
  return Math.hypot(card.x + card.w - pointer.x, card.y + card.h - pointer.y) < RESIZE_GRAB_RADIUS;
}
