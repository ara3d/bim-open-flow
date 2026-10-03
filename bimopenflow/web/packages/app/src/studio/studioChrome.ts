// The studio look (a prototype beside the classic one): the same controller
// and shared content behind chrome.ts, in a frame meant to explain itself.
//
// - A command bar with the flow's title (flowTitle.ts) as the heading, one
//   primary Run button, Save with an unsaved dot, a View menu holding the
//   canvas theme and node style pickers, and a connection dot.
// - A left column with the shared sidebar (Steps / Nodes).
// - The canvas with a floating toolbar (Fit, Tidy, Add node), an empty-state
//   card when the flow has no nodes, and a "?" popover for the gestures.
// - A right column with the Ask host over the pane area.
//
// Layout is studio.css; the shared content keeps its bof-app- classes and
// the stylesheet restyles them under .bof-studio. The studio keeps its own
// canvas theme choice (studio by default), so a look never leaks its theme
// into the other.

import { canvasThemeNames, isCanvasThemeName, isNodeStyleName, nodeStyleNames } from "@bimopenflow/graph";
import type { AppChrome, ChromeActions, ChromeFactory } from "../chrome.js";
import { installSplitter, LEFT_SPLIT, restoreWidth, RIGHT_SPLIT } from "../columnSplitter.js";
import { createSidebar } from "../sidebar.js";
import { TEMPLATES } from "../templates.generated.js";
import type { FlowTemplate } from "../templates.js";
import { namePicker } from "../topbar.js";
import type { ThemePrefs } from "../themeChoice.js";
import { brandMark, svgIcon } from "../brand.js";
import { flowTitle } from "./flowTitle.js";
import "./studio.css";

export const STUDIO_THEME_PREFS: ThemePrefs = { key: "bof-studio-canvas-theme", fallback: "studio" };

/** What the Ask box offers before anyone types. */
export const STUDIO_ASK_EXAMPLES: readonly string[] = [
  "Add a bar chart of rooms per storey",
  "Colour the doors by width",
  "Which walls have no fire rating?",
  "Filter this table to the ground floor",
  "What does spatial.intersects do?",
];

const CONNECTION_LABEL = { connected: "Connected", reconnecting: "Reconnecting…", offline: "Offline" } as const;

export interface StudioChromeOptions {
  /** The sample catalog for flow titles; the generated one by default. */
  templates?: readonly FlowTemplate[];
}

const icon = svgIcon;

const ICON = {
  fit: "M3 9V3h6v2H5v4Zm12-6h6v6h-2V5h-4ZM3 15h2v4h4v2H3Zm16 0h2v6h-6v-2h4Z",
  tidy: "M3 5h6v4H3Zm0 10h6v4H3Zm12-5h6v4h-6ZM9 7h3v4l3-1V9h0M9 17h3v-4l3 1v1",
  plus: "M11 5h2v6h6v2h-6v6h-2v-6H5v-2h6Z",
  play: "M8 5v14l11-7Z",
  save: "M5 3h11l3 3v15H5Zm2 2v5h8V5Zm1 9v5h8v-5Z",
  gear: "M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm8.9 4-.1-1.4 2-1.5-2-3.5-2.4 1a8 8 0 0 0-2.4-1.4L15.5 2h-4l-.5 2.6a8 8 0 0 0-2.4 1.4l-2.4-1-2 3.5 2 1.5L6.1 12l.1 1.4-2 1.5 2 3.5 2.4-1a8 8 0 0 0 2.4 1.4l.5 2.6h4l.5-2.6a8 8 0 0 0 2.4-1.4l2.4 1 2-3.5-2-1.5Z",
  help: "M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20Zm0 15.5a1.2 1.2 0 1 1 0-2.4 1.2 1.2 0 0 1 0 2.4Zm1-5.2V13h-2v-1.3c0-1.6 2.6-1.8 2.6-3.4 0-.8-.7-1.4-1.6-1.4s-1.7.6-1.7 1.6H8.3c0-2.1 1.6-3.5 3.7-3.5s3.7 1.3 3.7 3.3c0 2.3-2.7 2.5-2.7 4Z",
} as const;

export const studioChrome = (options: StudioChromeOptions = {}): ChromeFactory =>
  (root, actions) => {
    const doc = root.ownerDocument;
    const templates = options.templates ?? TEMPLATES;
    root.classList.add("bof-studio");

    const el = <K extends keyof HTMLElementTagNameMap>(tag: K, className: string, text?: string): HTMLElementTagNameMap[K] => {
      const e = doc.createElement(tag);
      e.className = className;
      if (text !== undefined) e.textContent = text;
      return e;
    };
    const button = (className: string, label: string, iconPath: string, onClick: () => void, title = label): HTMLButtonElement => {
      const b = el("button", `bof-studio-btn ${className}`);
      b.type = "button";
      b.title = title;
      b.setAttribute("aria-label", label);
      b.append(icon(doc, iconPath), el("span", "bof-studio-btn-label", label));
      b.addEventListener("click", onClick);
      return b;
    };

    // ── command bar ──────────────────────────────────────────────────────────
    const top = el("header", "bof-studio-top");
    const brand = el("div", "bof-studio-brand");
    brand.append(brandMark(doc), el("span", "bof-studio-wordmark", "BimOpenFlow"));

    const flow = el("div", "bof-studio-flow");
    const picker = el("select", "bof-studio-picker");
    picker.setAttribute("aria-label", "Open flow");
    picker.addEventListener("change", () => { if (picker.value) actions.openAnalysis(picker.value); });
    const flowMeta = el("div", "bof-studio-flow-meta");
    const flowId = el("span", "bof-studio-flow-id");
    const flowCount = el("span", "bof-studio-flow-count");
    flowMeta.append(flowId, flowCount);
    flow.append(picker, flowMeta);

    const newBtn = button("bof-studio-btn-quiet", "New", ICON.plus, actions.newAnalysis, "Start from a template or a blank flow");
    const saveBtn = button("bof-studio-btn-quiet bof-studio-save", "Save", ICON.save, actions.save);
    const dirtyDot = el("span", "bof-studio-dirty");
    dirtyDot.title = "Unsaved changes";
    saveBtn.append(dirtyDot);
    const runBtn = button("bof-studio-btn-primary", "Run", ICON.play, actions.run, "Run: write every output and record the run");

    const menu = el("details", "bof-studio-menu");
    const menuSummary = el("summary", "bof-studio-btn bof-studio-btn-quiet");
    menuSummary.setAttribute("aria-label", "View settings");
    menuSummary.append(icon(doc, ICON.gear), el("span", "bof-studio-btn-label", "View"));
    const menuBody = el("div", "bof-studio-menu-body");
    const themePicker = namePicker(doc, "Canvas theme", canvasThemeNames, isCanvasThemeName, actions.setTheme);
    const stylePicker = namePicker(doc, "Node style", nodeStyleNames, isNodeStyleName, actions.setNodeStyle);
    const field = (label: string, control: HTMLElement) => {
      const l = el("label", "bof-studio-field");
      l.append(el("span", "bof-studio-field-label", label), control);
      return l;
    };
    menuBody.append(field("Canvas theme", themePicker), field("Node style", stylePicker));
    menu.append(menuSummary, menuBody);

    const status = el("span", "bof-studio-status");
    const statusDot = el("i", "bof-studio-status-dot");
    const statusText = el("span", "bof-studio-status-text");
    status.append(statusDot, statusText);

    top.append(brand, flow, newBtn, el("span", "bof-studio-spacer"), saveBtn, runBtn, menu, status);

    // ── columns ──────────────────────────────────────────────────────────────
    const main = el("div", "bof-studio-main");
    const left = el("aside", "bof-studio-left");
    const sidebarEl = el("div", "");
    left.append(sidebarEl);
    const sidebar = createSidebar(sidebarEl, actions.addNode);

    const leftSplit = el("div", "bof-app-splitter bof-studio-splitter");
    const leftCfg = { ...LEFT_SPLIT, storageKey: "bof-studio-left-width", fallback: 300 };
    installSplitter(leftSplit, root, leftCfg);

    const canvasHost = el("div", "bof-studio-canvas-host");
    const canvas = doc.createElement("canvas");
    const tools = el("div", "bof-studio-canvas-tools");
    tools.setAttribute("role", "toolbar");
    tools.setAttribute("aria-label", "Canvas");
    tools.append(
      button("", "Fit", ICON.fit, actions.fit, "Fit the whole flow in view"),
      button("", "Tidy", ICON.tidy, actions.tidy, "Lay the steps out left to right"),
      button("", "Add node", ICON.plus, () => sidebar.showTab("nodes"), "Open the node catalog (or press Space over the canvas)"),
    );

    const empty = el("div", "bof-studio-empty");
    empty.hidden = true;
    const emptyCard = el("div", "bof-studio-empty-card");
    emptyCard.append(
      el("h2", "bof-studio-empty-title", "This flow has no steps yet"),
      el("p", "bof-studio-empty-text", "A flow is a chain of small steps over a model's tables. Start one of three ways:"),
    );
    const emptyActions = el("div", "bof-studio-empty-actions");
    const emptyAction = (label: string, hint: string, act: () => void) => {
      const b = el("button", "bof-studio-empty-action");
      b.type = "button";
      b.append(el("strong", "", label), el("span", "", hint));
      b.addEventListener("click", act);
      return b;
    };
    emptyActions.append(
      emptyAction("Start from a template", "A door schedule, rooms per storey, a rule check", actions.newAnalysis),
      emptyAction("Add a node", "Pick a source, a table step, a chart, or a 3D view", () => sidebar.showTab("nodes")),
      emptyAction("Ask Claude", "Describe what you want and it builds the steps", () => askHost.querySelector("input")?.focus()),
    );
    emptyCard.append(emptyActions);
    empty.append(emptyCard);

    const help = el("details", "bof-studio-help");
    const helpSummary = el("summary", "bof-studio-btn bof-studio-btn-round");
    helpSummary.setAttribute("aria-label", "Canvas gestures");
    helpSummary.append(icon(doc, ICON.help));
    const helpBody = el("dl", "bof-studio-help-body");
    for (const [gesture, effect] of [
      ["Drag a node", "move it"], ["Drag from a socket", "wire it"], ["Drop a wire on empty canvas", "add a matching node"],
      ["Click a wire, Delete", "cut it"], ["Right-click or Space", "node palette"], ["Hover a wire", "peek at its rows"],
      ["Double-click a node", "show it in the pane"], ["Wheel, drag the background", "zoom, pan"],
    ]) {
      helpBody.append(el("dt", "", gesture!), el("dd", "", effect!));
    }
    help.append(helpSummary, helpBody);
    canvasHost.append(canvas, tools, empty, help);

    const rightSplit = el("div", "bof-app-splitter bof-studio-splitter");
    // A third of the window for the panes, so a 3D view opens at a useful size.
    const rightCfg = { ...RIGHT_SPLIT, storageKey: "bof-studio-right-width", fallback: Math.max(420, Math.round((doc.defaultView?.innerWidth ?? 1400) * 0.34)) };
    installSplitter(rightSplit, root, rightCfg);

    const right = el("div", "bof-studio-right");
    const askHost = el("div", "bof-studio-ask-host");
    const paneEl = el("div", "");
    right.append(askHost, paneEl);

    main.append(left, leftSplit, canvasHost, rightSplit, right);
    root.append(top, main);
    const resize = () => { restoreWidth(root, leftCfg); restoreWidth(root, rightCfg); };
    resize();
    doc.defaultView?.addEventListener("resize", resize);

    const chrome: AppChrome = {
      canvas, canvasHost, paneEl, askHost,
      stepsEl: sidebar.stepsEl,
      themePrefs: STUDIO_THEME_PREFS,
      askExamples: STUDIO_ASK_EXAMPLES,
      ownsCanvasHint: true,
      setAnalyses(list, activeId) {
        picker.replaceChildren(...(list.length ? [] : [new Option("No flows on this host", "")]), ...list.map((a) => {
          const option = new Option(flowTitle(a.id, templates), a.id, false, a.id === activeId);
          return option;
        }));
        flowId.textContent = activeId ?? "";
        doc.title = activeId ? `${flowTitle(activeId, templates)} · BimOpenFlow Studio` : "BimOpenFlow Studio";
      },
      setDirty(dirty) {
        root.classList.toggle("bof-studio-is-dirty", dirty);
        saveBtn.disabled = !dirty;
      },
      setConnection(status) {
        statusText.textContent = CONNECTION_LABEL[status];
        root.dataset.connection = status;
        statusDot.title = CONNECTION_LABEL[status];
      },
      setTheme: (name) => { themePicker.value = name; },
      setNodeStyle: (name) => { stylePicker.value = name; },
      setCatalog: (nodes) => sidebar.setCatalog([...nodes]),
      flowOpened: sidebar.flowOpened,
      setNodes(nodes) {
        empty.hidden = nodes.length > 0;
        flowCount.textContent = nodes.length === 0 ? "" : `${nodes.length} ${nodes.length === 1 ? "step" : "steps"}`;
      },
      setShownNode() {},
      dispose: () => doc.defaultView?.removeEventListener("resize", resize),
    };
    return chrome;
  };
