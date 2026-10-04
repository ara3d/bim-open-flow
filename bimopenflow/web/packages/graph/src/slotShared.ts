// Island plumbing shared by every on-node control: canvasControls.ts (field,
// number, column), canvasLongSlot.ts (long text). Islands are real DOM
// elements glued to a node via gratify's island facet; they live outside
// gratify's intent flow, so their commits need a row key (here), a dispatch
// (their CanvasInstance's), and a theme-matched style (here).

import { css, type Tokens } from "gratify";
import { currentCanvasTheme } from "./canvasTheme.js";

/** Key of one parameter row: "nodeId::name". */
export const islandKey = (nodeId: string, name: string): string => `${nodeId}::${name}`;

/** Border, background, font, and focus colour from the canvas theme. Sets
 *  each property on its own: gratify's runtime pins the element over its
 *  world rect with position, offsets, and a top-left transform origin it
 *  writes once, and rewriting the whole inline style (as a theme switch
 *  used to) dropped them, so every island scaled about its centre. Width
 *  and height are set only on a fresh element; the runtime owns them after. */
export function styleIsland(el: HTMLInputElement | HTMLTextAreaElement, palette: Omit<Tokens, "mix">): void {
  const s = el.style;
  s.boxSizing = "border-box";
  if (s.width === "") s.width = "100%";
  if (s.height === "") s.height = "100%";
  s.borderRadius = "5px";
  s.padding = "0 7px";
  s.font = "14px system-ui,'Segoe UI',sans-serif";
  s.outline = "none";
  s.border = `1px solid ${css(palette.muted)}`;
  s.background = css(palette.bg);
  s.color = css(palette.text);
  s.colorScheme = currentCanvasTheme().includes("light") ? "light" : "dark";
  el.onfocus = () => (el.style.borderColor = css(palette.accent));
  el.onblur = () => (el.style.borderColor = css(palette.muted));
}
