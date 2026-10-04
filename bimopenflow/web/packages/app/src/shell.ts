// The classic shell layout: topbar over sidebar | splitter | canvas | splitter
// | right column, where the right column stacks an Ask host (empty unless the
// host has /api/ask, TKT-112) over the pane area. Plain DOM under the
// bof-app- prefix; the splitters are columnSplitter.ts's.

import { ensureAppStyles } from "./styles.js";
import { installSplitter, LEFT_SPLIT, MIN_CANVAS, restoreWidth, RIGHT_SPLIT } from "./columnSplitter.js";

export interface Shell {
  topbarEl: HTMLElement;
  sidebarEl: HTMLElement;
  canvas: HTMLCanvasElement;
  /** The positioned box around the canvas; overlays (the start page, the
   *  problems strip) mount here so they cover the canvas and nothing else. */
  canvasHost: HTMLElement;
  paneEl: HTMLElement;
  /** Top of the right column, above the pane area. Empty (and so taking no
   *  room) until the Ask panel mounts into it. */
  askHost: HTMLElement;
  graphToolbar: HTMLElement;
  dispose(): void;
}

export function buildShell(root: HTMLElement, graphDemo = false): Shell {
  ensureAppStyles(root.ownerDocument);
  const doc = root.ownerDocument;
  root.classList.add("bof-app-root");
  root.classList.toggle("bof-app-graph-demo", graphDemo);
  const right = graphDemo ? {
    ...RIGHT_SPLIT,
    storageKey: "bof-demo-right-width",
    fallback: Math.round(window.innerWidth * 0.5),
    max: () => Math.max(240, window.innerWidth - 6 - MIN_CANVAS),
  } : RIGHT_SPLIT;
  if (graphDemo) root.style.setProperty("--bof-app-right", `${right.fallback}px`);

  const topbarEl = doc.createElement("div");
  const main = doc.createElement("div");
  main.className = "bof-app-main";

  const sidebarEl = doc.createElement("div");

  const leftSplitter = doc.createElement("div");
  leftSplitter.className = "bof-app-splitter";
  leftSplitter.hidden = graphDemo;
  installSplitter(leftSplitter, root, LEFT_SPLIT);

  const canvasHost = doc.createElement("div");
  canvasHost.className = "bof-app-canvas-host";
  const graphToolbar = doc.createElement("div");
  graphToolbar.className = "bof-app-graph-toolbar";
  graphToolbar.hidden = !graphDemo;
  canvasHost.appendChild(graphToolbar);
  const canvas = doc.createElement("canvas");
  canvasHost.appendChild(canvas);

  const rightSplitter = doc.createElement("div");
  rightSplitter.className = "bof-app-splitter";
  installSplitter(rightSplitter, root, right);

  const rightColumn = doc.createElement("div");
  rightColumn.className = "bof-app-right-column";
  const askHost = doc.createElement("div");
  askHost.className = "bof-app-ask-host";
  const paneEl = doc.createElement("div");
  rightColumn.append(askHost, paneEl);

  main.append(sidebarEl, leftSplitter, canvasHost, rightSplitter, rightColumn);
  root.append(topbarEl, main);
  if (!graphDemo) restoreWidth(root, LEFT_SPLIT);
  restoreWidth(root, right);
  // Re-clamp on window resize so the canvas column never collapses to zero.
  const resize = () => {
    if (!graphDemo) restoreWidth(root, LEFT_SPLIT);
    restoreWidth(root, right);
  };
  root.ownerDocument.defaultView?.addEventListener("resize", resize);
  return { topbarEl, sidebarEl, canvas, canvasHost, paneEl, askHost, graphToolbar,
    dispose: () => root.ownerDocument.defaultView?.removeEventListener("resize", resize),
  };
}
