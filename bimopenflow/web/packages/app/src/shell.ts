// The shell layout: topbar over sidebar | canvas | splitter | pane area.
// Plain DOM under the bof-app- prefix; the splitter drags the pane-area
// width via the --bof-app-right custom property.

import { ensureAppStyles } from "./styles.js";

export interface Shell {
  topbarEl: HTMLElement;
  sidebarEl: HTMLElement;
  canvas: HTMLCanvasElement;
  paneEl: HTMLElement;
}

export function buildShell(root: HTMLElement): Shell {
  ensureAppStyles(root.ownerDocument);
  const doc = root.ownerDocument;
  root.classList.add("bof-app-root");

  const topbarEl = doc.createElement("div");
  const main = doc.createElement("div");
  main.className = "bof-app-main";

  const sidebarEl = doc.createElement("div");

  const canvasHost = doc.createElement("div");
  canvasHost.className = "bof-app-canvas-host";
  const canvas = doc.createElement("canvas");
  canvasHost.appendChild(canvas);

  const splitter = doc.createElement("div");
  splitter.className = "bof-app-splitter";
  installSplitter(splitter, root);

  const paneEl = doc.createElement("div");

  main.append(sidebarEl, canvasHost, splitter, paneEl);
  root.append(topbarEl, main);
  return { topbarEl, sidebarEl, canvas, paneEl };
}

/** Dragging the splitter resizes the pane column (min 240, max 70vw). */
function installSplitter(splitter: HTMLElement, root: HTMLElement): void {
  splitter.addEventListener("pointerdown", (down) => {
    down.preventDefault();
    splitter.setPointerCapture(down.pointerId);
    const startX = down.clientX;
    const startWidth =
      parseFloat(getComputedStyle(root).getPropertyValue("--bof-app-right")) || 420;
    const move = (e: PointerEvent) => {
      const width = Math.min(
        window.innerWidth * 0.7,
        Math.max(240, startWidth + (startX - e.clientX)),
      );
      root.style.setProperty("--bof-app-right", `${width}px`);
    };
    const up = () => {
      splitter.removeEventListener("pointermove", move);
      splitter.removeEventListener("pointerup", up);
    };
    splitter.addEventListener("pointermove", move);
    splitter.addEventListener("pointerup", up);
  });
}
