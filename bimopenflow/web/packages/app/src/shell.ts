// The shell layout: topbar over sidebar | splitter | canvas | splitter | pane
// area. Plain DOM under the bof-app- prefix. Splitters drag a ghost line and
// apply the column width once on release — resizing the columns live would
// resize the <canvas> bitmap on every pointermove, which clears it until the
// next gratify frame and makes the canvas flash. Widths persist per splitter
// in localStorage.

import { ensureAppStyles } from "./styles.js";
import { readPref, writePref } from "./prefs.js";
import { clampWidth, dragWidth, ghostX, type SplitSpec } from "./splitMath.js";

export interface Shell {
  topbarEl: HTMLElement;
  sidebarEl: HTMLElement;
  canvas: HTMLCanvasElement;
  paneEl: HTMLElement;
}

interface SplitterConfig {
  cssVar: string;
  storageKey: string;
  sign: 1 | -1;
  min: number;
  max(root: HTMLElement): number;
  fallback: number;
}

// The canvas column is the grid remainder; both splitter maxima subtract the
// other column so it can never collapse to zero (narrow-window regression:
// 240px sidebar + 420px pane area left a 0px canvas at ~670px windows).
const MIN_CANVAS = 220;
const SPLITTER_TOTAL = 12;

const columnWidth = (root: HTMLElement, cssVar: string, fallback: number): number =>
  parseFloat(getComputedStyle(root).getPropertyValue(cssVar)) || fallback;

const LEFT: SplitterConfig = {
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

const RIGHT: SplitterConfig = {
  cssVar: "--bof-app-right",
  storageKey: "bof-app-right-width",
  sign: -1,
  min: 240,
  max: (root) => Math.max(240,
    window.innerWidth - columnWidth(root, "--bof-app-left", 240) - SPLITTER_TOTAL - MIN_CANVAS),
  fallback: 420,
};

export function buildShell(root: HTMLElement): Shell {
  ensureAppStyles(root.ownerDocument);
  const doc = root.ownerDocument;
  root.classList.add("bof-app-root");

  const topbarEl = doc.createElement("div");
  const main = doc.createElement("div");
  main.className = "bof-app-main";

  const sidebarEl = doc.createElement("div");

  const leftSplitter = doc.createElement("div");
  leftSplitter.className = "bof-app-splitter";
  installSplitter(leftSplitter, root, LEFT);

  const canvasHost = doc.createElement("div");
  canvasHost.className = "bof-app-canvas-host";
  const canvas = doc.createElement("canvas");
  canvasHost.appendChild(canvas);

  const rightSplitter = doc.createElement("div");
  rightSplitter.className = "bof-app-splitter";
  installSplitter(rightSplitter, root, RIGHT);

  const paneEl = doc.createElement("div");

  main.append(sidebarEl, leftSplitter, canvasHost, rightSplitter, paneEl);
  root.append(topbarEl, main);
  restoreWidth(root, LEFT);
  restoreWidth(root, RIGHT);
  // Re-clamp on window resize so the canvas column never collapses to zero.
  root.ownerDocument.defaultView?.addEventListener("resize", () => {
    restoreWidth(root, LEFT);
    restoreWidth(root, RIGHT);
  });
  return { topbarEl, sidebarEl, canvas, paneEl };
}

function restoreWidth(root: HTMLElement, cfg: SplitterConfig): void {
  const saved = parseFloat(readPref(cfg.storageKey) ?? "");
  const width = Number.isFinite(saved) ? saved : columnWidth(root, cfg.cssVar, cfg.fallback);
  root.style.setProperty(cfg.cssVar, `${clampWidth(width, cfg.min, cfg.max(root))}px`);
}

/** Ghost-line drag: track the pointer with a fixed overlay line, then set the
 *  CSS column variable once on pointerup (see the module comment for why). */
function installSplitter(
  splitter: HTMLElement,
  root: HTMLElement,
  cfg: SplitterConfig,
): void {
  splitter.addEventListener("pointerdown", (down) => {
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
    const up = () => {
      splitter.removeEventListener("pointermove", move);
      splitter.removeEventListener("pointerup", up);
      ghost.remove();
      root.style.setProperty(cfg.cssVar, `${width}px`);
      writePref(cfg.storageKey, String(Math.round(width)));
    };
    splitter.addEventListener("pointermove", move);
    splitter.addEventListener("pointerup", up);
  });
}
