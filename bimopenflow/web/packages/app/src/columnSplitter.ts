// Column splitters shared by the layouts (shell.ts, studio/studioChrome.ts).
// A splitter drags a ghost line and applies the column width once on release:
// resizing the columns live would resize the <canvas> bitmap on every
// pointermove, which clears it until the next gratify frame and makes the
// canvas flash. Widths persist per splitter in localStorage.

import { readPref, writePref } from "./prefs.js";
import { clampWidth, dragWidth, ghostX, type SplitSpec } from "./splitMath.js";

export interface SplitterConfig {
  /** The CSS custom property the column's grid track reads. */
  cssVar: string;
  storageKey: string;
  sign: 1 | -1;
  min: number;
  max(root: HTMLElement): number;
  fallback: number;
}

/** The canvas column is the grid remainder; both splitter maxima subtract the
 *  other column so it can never collapse to zero (narrow-window regression:
 *  240px sidebar + 420px pane area left a 0px canvas at ~670px windows). */
export const MIN_CANVAS = 220;
export const SPLITTER_TOTAL = 12;

export const columnWidth = (root: HTMLElement, cssVar: string, fallback: number): number =>
  parseFloat(getComputedStyle(root).getPropertyValue(cssVar)) || fallback;

/** The left column of a three-column layout, under `--bof-app-left`. */
export const LEFT_SPLIT: SplitterConfig = {
  cssVar: "--bof-app-left",
  storageKey: "bof-app-left-width",
  sign: 1,
  min: 160,
  max: (root) => Math.max(160, Math.min(
    520,
    window.innerWidth - columnWidth(root, "--bof-app-right", 420) - SPLITTER_TOTAL - MIN_CANVAS,
  )),
  fallback: 240,
};

/** The right column, under `--bof-app-right`. */
export const RIGHT_SPLIT: SplitterConfig = {
  cssVar: "--bof-app-right",
  storageKey: "bof-app-right-width",
  sign: -1,
  min: 240,
  max: (root) => Math.max(240,
    window.innerWidth - columnWidth(root, "--bof-app-left", 240) - SPLITTER_TOTAL - MIN_CANVAS),
  fallback: 420,
};

/** Sets the column's CSS variable from the saved width (else the fallback),
 *  clamped to the config's range. Call again on window resize: the clamp
 *  starts from the wanted width each time, so a column squeezed by a narrow
 *  window grows back when the window does. */
export function restoreWidth(root: HTMLElement, cfg: SplitterConfig): void {
  const saved = parseFloat(readPref(cfg.storageKey) ?? "");
  const width = Number.isFinite(saved) ? saved : cfg.fallback;
  root.style.setProperty(cfg.cssVar, `${clampWidth(width, cfg.min, cfg.max(root))}px`);
}

/** Ghost-line drag: track the pointer with a fixed overlay line, then set the
 *  CSS column variable once on pointerup (see the module comment for why). */
export function installSplitter(splitter: HTMLElement, root: HTMLElement, cfg: SplitterConfig): void {
  splitter.addEventListener("pointerdown", (down) => {
    if (down.button !== 0) return;
    down.preventDefault();
    splitter.setPointerCapture(down.pointerId);
    const doc = root.ownerDocument;
    const startX = down.clientX;
    const startWidth =
      parseFloat(getComputedStyle(root).getPropertyValue(cfg.cssVar)) || cfg.fallback;
    const spec: SplitSpec = { min: cfg.min, max: cfg.max(root), sign: cfg.sign };

    const ghost = doc.createElement("div");
    ghost.className = "bof-app-split-ghost";
    ghost.style.left = `${startX}px`;
    doc.body.appendChild(ghost);

    let width = startWidth;
    const move = (e: PointerEvent) => {
      width = dragWidth(startWidth, startX, e.clientX, spec);
      ghost.style.left = `${ghostX(startX, startWidth, width, spec.sign)}px`;
    };
    const finish = (apply: boolean) => {
      splitter.removeEventListener("pointermove", move);
      splitter.removeEventListener("pointerup", up);
      splitter.removeEventListener("pointercancel", cancel);
      splitter.removeEventListener("lostpointercapture", cancel);
      ghost.remove();
      if (!apply) return;
      root.style.setProperty(cfg.cssVar, `${width}px`);
      writePref(cfg.storageKey, String(Math.round(width)));
    };
    const up = () => finish(true);
    const cancel = () => finish(false);
    splitter.addEventListener("pointermove", move);
    splitter.addEventListener("pointerup", up);
    splitter.addEventListener("pointercancel", cancel);
    splitter.addEventListener("lostpointercapture", cancel);
  });
}
