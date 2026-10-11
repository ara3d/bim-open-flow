// The offer after "Share as source" (TKT-138): the other nodes holding the same
// path, listed, with one button that links them all. A small panel at the
// pointer; Escape, "Not now", or a click elsewhere closes it.

import type { RelinkTarget } from "./sourcePlans.js";

export interface RelinkOfferOptions {
  readonly document: Document;
  /** Where the panel's top-left corner goes, in client pixels. */
  readonly at: { readonly x: number; readonly y: number };
  readonly source: string;
  readonly targets: readonly RelinkTarget[];
  /** The user accepted: link every target. */
  onAccept(): void;
}

/** Shows the panel; returns a function that closes it. */
export function showRelinkOffer(options: RelinkOfferOptions): () => void {
  const { document: doc, targets } = options;
  const panel = doc.createElement("div");
  panel.setAttribute("role", "dialog");
  panel.setAttribute("aria-label", "Link other nodes to the source");
  panel.style.cssText = "position:fixed;z-index:10002;max-width:320px;padding:10px 12px;background:#fff;color:#182431;" +
    "border:1px solid #c8d2dc;border-radius:8px;box-shadow:0 5px 20px #13253626;font:13px/1.4 system-ui,sans-serif;" +
    `left:${options.at.x}px;top:${options.at.y}px`;
  const heading = doc.createElement("div");
  heading.textContent = `${targets.length} other ${targets.length === 1 ? "node holds" : "nodes hold"} the same path. Link ${targets.length === 1 ? "it" : "them"} to ${options.source}?`;
  heading.style.fontWeight = "600";
  const list = doc.createElement("ul");
  list.style.cssText = "margin:6px 0;padding-left:18px;max-height:160px;overflow:auto";
  for (const target of targets) {
    const item = doc.createElement("li");
    item.textContent = `${target.nodeId}.${target.param}`;
    list.append(item);
  }
  const buttons = doc.createElement("div");
  buttons.style.cssText = "display:flex;gap:8px;justify-content:flex-end";
  const accept = doc.createElement("button");
  accept.type = "button";
  accept.textContent = targets.length === 1 ? "Link it" : "Link all";
  const decline = doc.createElement("button");
  decline.type = "button";
  decline.textContent = "Not now";
  buttons.append(decline, accept);
  panel.append(heading, list, buttons);
  doc.body.append(panel);

  let closed = false;
  const close = () => {
    if (closed) return;
    closed = true;
    doc.removeEventListener("pointerdown", outside, true);
    doc.removeEventListener("keydown", onKey, true);
    panel.remove();
  };
  const outside = (event: Event) => { if (!panel.contains(event.target as Node)) close(); };
  const onKey = (event: KeyboardEvent) => { if (event.key === "Escape") { event.stopPropagation(); close(); } };
  accept.addEventListener("click", () => { close(); options.onAccept(); });
  decline.addEventListener("click", close);
  doc.addEventListener("pointerdown", outside, true);
  doc.addEventListener("keydown", onKey, true);
  accept.focus();
  return close;
}
