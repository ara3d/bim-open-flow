// Node resize geometry (TKT-124): where the corner grip sits. The size
// bounds a drag is held to live in nodeSize.ts. Pure and gratify-free.

import { grabRadius } from "./portGeometry.js";

/** Radius around a card's bottom-right corner that starts a resize: world
 *  units at zoom 1 and above, screen pixels below (portGeometry.grabRadius).
 *  A press nearer a socket than the corner goes to the socket instead
 *  (resizeHandleHit), so a param-less card's last output socket, 12 units
 *  above the corner, still starts a wire. */
export const RESIZE_GRAB_RADIUS = 10;

type Point = { readonly x: number; readonly y: number };

/** True when `pointer` is within RESIZE_GRAB_RADIUS of `card`'s bottom-right
 *  corner at `zoom`, and nearer the corner than any of `sockets`. The
 *  radius is at most half the card's shorter side, so a card drawn a few
 *  pixels wide far out still moves when pressed. Points are world units. */
export function resizeHandleHit(
  card: { readonly x: number; readonly y: number; readonly w: number; readonly h: number },
  pointer: Point,
  zoom = 1,
  sockets: readonly Point[] = [],
): boolean {
  const corner = Math.hypot(card.x + card.w - pointer.x, card.y + card.h - pointer.y);
  const radius = Math.min(grabRadius(RESIZE_GRAB_RADIUS, zoom), Math.min(card.w, card.h) / 2);
  return corner < radius && sockets.every((s) => Math.hypot(s.x - pointer.x, s.y - pointer.y) > corner);
}
