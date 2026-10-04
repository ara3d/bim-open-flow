// The classic look: today's shell (shell.ts), topbar (topbar.ts), and
// sidebar (sidebar.ts) behind the chrome seam (chrome.ts). With `graphDemo`
// the sidebar hides behind a Nodes button and a graph toolbar carries Fit,
// a readable-size button, and the Preview node picker (TKT-113), the way
// 3d.html and the DuckDB demo have always shown them.

import type { AppChrome, ChromeActions, ChromeFactory } from "./chrome.js";
import { buildShell } from "./shell.js";
import { createSidebar } from "./sidebar.js";
import { createTopbar } from "./topbar.js";

export interface ClassicChromeOptions {
  graphDemo?: boolean;
  /** Topbar heading; defaults to the BimOpenFlow / Snowdon 3D link. */
  heading?: string;
}

const CATALOG_OPEN = "bof-app-catalog-open";

export const classicChrome = (options: ClassicChromeOptions = {}): ChromeFactory =>
  (root, actions) => {
    const doc = root.ownerDocument;
    const shell = buildShell(root, options.graphDemo);
    const topbar = createTopbar(shell.topbarEl, {
      heading: options.heading,
      onOpenAnalysis: actions.openAnalysis,
      onNewAnalysis: actions.newAnalysis,
      onSave: actions.save,
      onRun: actions.run,
      onThemeChange: actions.setTheme,
      onNodeStyleChange: actions.setNodeStyle,
    });
    const sidebar = createSidebar(shell.sidebarEl, (desc) => {
      actions.addNode(desc);
      closeCatalog();
    });

    const nodesBtn = doc.createElement("button");
    const closeCatalog = () => {
      root.classList.remove(CATALOG_OPEN);
      nodesBtn.setAttribute("aria-expanded", "false");
    };

    const preview = doc.createElement("select");
    preview.setAttribute("aria-label", "Preview node");
    preview.addEventListener("change", () => actions.showInPane(preview.value));

    if (options.graphDemo) {
      nodesBtn.textContent = "Nodes";
      nodesBtn.setAttribute("aria-expanded", "false");
      nodesBtn.addEventListener("click", () => {
        const open = root.classList.toggle(CATALOG_OPEN);
        nodesBtn.setAttribute("aria-expanded", String(open));
        if (open) sidebar.showTab("nodes");
      });
      const fit = doc.createElement("button");
      fit.textContent = "Fit graph";
      fit.addEventListener("click", actions.fit);
      const label = doc.createElement("label");
      label.append("Preview ", preview);
      const readable = doc.createElement("button");
      readable.textContent = "100%";
      readable.title = "Readable size: focus the preview node";
      readable.addEventListener("click", () => actions.selectAndFocus(preview.value));
      shell.graphToolbar.append(nodesBtn, fit, readable, label);
    }

    const chrome: AppChrome = {
      canvas: shell.canvas,
      canvasHost: shell.canvasHost,
      paneEl: shell.paneEl,
      askHost: shell.askHost,
      stepsEl: sidebar.stepsEl,
      setAnalyses: (list, activeId) => topbar.setAnalyses([...list], activeId),
      setDirty: topbar.setDirty,
      setConnection: topbar.setConnection,
      setTheme: topbar.setTheme,
      setNodeStyle: topbar.setNodeStyle,
      setCatalog: (nodes) => sidebar.setCatalog([...nodes]),
      flowOpened: sidebar.flowOpened,
      setNodes(nodes) {
        if (!options.graphDemo) return;
        const shown = preview.value;
        preview.replaceChildren(...nodes.map((n) => {
          const option = doc.createElement("option");
          option.value = n.id;
          option.textContent = `${n.title} · ${n.id}`;
          return option;
        }));
        preview.value = shown;
      },
      setShownNode: (nodeId) => { preview.value = nodeId ?? ""; },
      dispose: shell.dispose,
    };
    return chrome;
  };
