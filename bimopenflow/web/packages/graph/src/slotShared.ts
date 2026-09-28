// Island plumbing shared by every on-node control: canvasControls.ts (field,
// number, column), canvasLongSlot.ts (long text). Islands are real DOM
// elements glued to a node via gratify's island facet; they live outside
// gratify's intent flow, so their commits need a row key, a registered
// dispatch, and a theme-matched style.

import { css, type Tokens } from "gratify";
import type { CanvasIntent } from "./canvasIntents.js";
import { currentCanvasTheme } from "./canvasTheme.js";

/** Key of one parameter row: "nodeId::name". */
export const islandKey = (nodeId: string, name: string): string => `${nodeId}::${name}`;

let dispatchIntent: (intent: CanvasIntent) => void = () => {};

/** canvasEditor registers the runtime's dispatch after mount. */
export function setInlineControlDispatch(fn: (intent: CanvasIntent) => void): void {
  dispatchIntent = fn;
}

/** DOM-side commits (islands, the long-value editor) enter the canvas intent flow here. */
export function dispatchInline(intent: CanvasIntent): void {
  dispatchIntent(intent);
}

/** Border, background, font, and focus colour from the canvas theme. */
export function styleIsland(el: HTMLInputElement | HTMLTextAreaElement, palette: Omit<Tokens, "mix">): void {
  el.style.cssText =
    "box-sizing:border-box;width:100%;height:100%;border-radius:5px;" +
    "padding:0 7px;font:14px system-ui,'Segoe UI',sans-serif;outline:none;" +
    `border:1px solid ${css(palette.muted)};` +
    `background:${css(palette.bg)};color:${css(palette.text)};`;
  el.style.colorScheme = currentCanvasTheme().includes("light") ? "light" : "dark";
  el.onfocus = () => (el.style.borderColor = css(palette.accent));
  el.onblur = () => (el.style.borderColor = css(palette.muted));
}
