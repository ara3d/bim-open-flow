import { calpha, mapRender, rgb, type Color, type PartExt } from "gratify";

/** A composable decoration: preserves the base renderer and all hit testing. */
export const selectionBorder = (color: () => Color, animate: () => boolean): PartExt =>
  mapRender((node, paint, _style, base) => {
    base();
    const selected = node.ch.sel ?? 0;
    if (selected < .01) return;
    const pulse = animate() ? .55 + .15 * Math.sin((node.time ?? 0) * Math.PI * 2 / 3) : .65;
    paint.box(node.rect.inset(-3), 10, rgb(0,0,0,0), calpha(color(),pulse*selected),1.5);
  });

export const animateSelection = (): boolean =>
  typeof window !== "undefined" && !(window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false);
