// The pane area: a tab strip plus one active pane for the selected node.
// Full docking is deferred by design (docs/bimopenflow-structure.md defers the
// docking/layout manager to gratify); one active pane + tabs covers the
// editor loop until then.

import type { NodeDescriptor, NodeState } from "@bimopenflow/contracts";
import {
  createChartPane,
  createInspectorPane,
  createTablePane,
  createVerdictPane,
  createViewPane3D,
  ensurePaneStyles,
  isBoxTable,
  type ChartPaneOptions,
  type Pane,
  type PaneContext,
  type PaneEvent,
} from "@bimopenflow/panes";
import {
  chartPaneOptions,
  choosePanes,
  firstTableOutput,
  studioPanes,
  hasResults,
  type PaneKind,
} from "./paneChoice.js";
import { completeTable } from "./completeTable.js";
import type { LiveViewRecipe } from "./liveViewRecipe";
import { nodeTitle } from "@bimopenflow/graph";

const PANE_LABELS: Record<PaneKind, string> = {
  verdict: "Verdicts",
  view3d: "3D",
  table: "Table",
  chart: "Chart",
  inspector: "Inspector",
};

const paneFactory = (kind: PaneKind, chartOptions: ChartPaneOptions): Pane => {
  switch (kind) {
    case "table": return createTablePane();
    case "chart": return createChartPane(chartOptions);
    case "verdict": return createVerdictPane();
    case "view3d": return createViewPane3D({ followGraph: true });
    case "inspector": return createInspectorPane();
  }
};

/** The host's own sentence from an ApiClient error ("GET <path> -> 404: {"error":"..."}"),
 *  or the whole message when it carries none. */
export function hostMessage(e: unknown): string {
  const message = e instanceof Error ? e.message : String(e);
  const body = message.indexOf("{");
  if (body < 0) return message;
  try {
    const parsed = JSON.parse(message.slice(body)) as { error?: unknown };
    return typeof parsed.error === "string" ? parsed.error : message;
  } catch {
    return message;
  }
}

interface ShownNode {
  nodeId: string;
  desc: NodeDescriptor | undefined;
  values: Record<string, string>;
  state: NodeState | undefined;
  /** Model file path feeding this node (see modelRef.modelPathFor); lets the
   * 3D pane load the model behind the instance/box tables. */
  modelPath?: string;
  pending?: boolean;
  live?: LiveViewRecipe;
  lineage?: string;
  /** True when this node is the flow's answer (TKT-46/TKT-81): the panes
   *  display it whether or not anything is selected. False means it is shown
   *  because of an explicit "show" request (double-click, the header's pin),
   *  not because it is the answer. */
  default?: boolean;
  /** True while the pane is pinned to this node regardless of the answer. */
  pinned?: boolean;
}

export interface PaneAreaDeps {
  /** The DuckDB studio's pane set (see studioPanes): the table, plus a Chart
   *  tab for chart.* nodes; the tab strip shows only when there is a choice. */
  tableOnly?: boolean;
  ctx: PaneContext;
  onSelect(ids: string[]): void;
  onError(message: string): void;
  /** Catalog model id for a node's model file path; null when unknown. */
  resolveModelId?(path: string): Promise<string | null>;
  /** Pane construction override for tests. */
  paneFactory?(kind: PaneKind, chartOptions: ChartPaneOptions): Pane;
  /** The pane header's pin toggle was clicked (TKT-81). */
  onTogglePin?(): void;
  /** The pane header's "Back to answer" button was clicked (TKT-81). */
  onShowAnswer?(): void;
}

export interface PaneArea {
  /** Shows the panes for a node; clears when nodeId is null. */
  showNode(shown: ShownNode | null): void;
  /** Re-feeds the active pane's data (e.g. after an eval update). */
  refreshData(): void;
  /** Mirrors the app selection into the active pane. */
  updateSelection(ids: string[]): void;
  dispose(): void;
}

export function createPaneArea(root: HTMLElement, deps: PaneAreaDeps): PaneArea {
  ensurePaneStyles(root.ownerDocument);
  root.classList.add("bof-app-panearea");
  const header = root.ownerDocument.createElement("div");
  header.className = "bof-app-pane-header";
  header.style.cssText = "display:flex;align-items:baseline;gap:8px;";
  const pinBtn = root.ownerDocument.createElement("button");
  pinBtn.type = "button";
  pinBtn.className = "bof-app-pin-toggle";
  pinBtn.title = "Pin: keep showing this node no matter what is selected or answered next";
  pinBtn.textContent = "Pin";
  pinBtn.setAttribute("aria-pressed", "false");
  pinBtn.hidden = true;
  pinBtn.style.cssText = "flex:none;";
  pinBtn.addEventListener("click", () => deps.onTogglePin?.());
  const source = root.ownerDocument.createElement("div");
  source.className = "bof-app-preview-source";
  source.setAttribute("role", "status");
  source.style.cssText = "flex:1 1 auto;min-width:0;";
  const backBtn = root.ownerDocument.createElement("button");
  backBtn.type = "button";
  backBtn.className = "bof-app-back-to-answer";
  backBtn.textContent = "Back to answer";
  backBtn.hidden = true;
  backBtn.style.cssText = "flex:none;";
  backBtn.addEventListener("click", () => deps.onShowAnswer?.());
  header.append(pinBtn, source, backBtn);
  /** One piece of the header line, classed so a look can style it. */
  const headerLine = (className: string, text: string): HTMLSpanElement => {
    const el = root.ownerDocument.createElement("span");
    el.className = className;
    el.textContent = text;
    return el;
  };
  const tabs = root.ownerDocument.createElement("div");
  tabs.className = "bof-app-tabs";
  const body = root.ownerDocument.createElement("div");
  body.className = "bof-app-panebody";
  root.append(header, tabs, body);

  let shown: ShownNode | null = null;
  let activeKind: PaneKind | null = null;
  let activePane: Pane | null = null;
  let activeChartOptions: ChartPaneOptions | null = null;
  let fetchToken = 0;
  let loadedModelUrl: string | null = null;

  const currentChartOptions = (): ChartPaneOptions =>
    chartPaneOptions(shown?.desc?.kind, shown?.values ?? {});

  // Chart options are small flat objects; JSON compare is enough (undefined
  // fields drop out on both sides).
  const sameChartOptions = (a: ChartPaneOptions, b: ChartPaneOptions): boolean =>
    JSON.stringify(a) === JSON.stringify(b);

  const destroyPane = () => {
    fetchToken++;
    activePane?.destroy();
    activePane = null;
    loadedModelUrl = null;
    body.textContent = "";
  };

  const showEmpty = (message: string) => {
    destroyPane();
    const empty = root.ownerDocument.createElement("div");
    empty.className = "bof-app-empty";
    empty.textContent = message;
    body.appendChild(empty);
  };

  const onPaneEvent = (e: PaneEvent) => {
    if (e.kind === "selection") deps.onSelect(e.event.ids);
    else if (e.action === "loadError") {
      loadedModelUrl = null;
      deps.onError(`3D model load failed: ${e.payload?.message ?? "unknown error"}`);
    }
  };

  // Loads the shown node's model into the 3D pane once per model: resolves the
  // node's model path to a catalog id and pushes { kind: "model" } before any
  // instance/box data, skipping when the same model is already loaded.
  const feedModel = async (pane: Pane, token: number) => {
    const path = shown?.modelPath;
    if (!path || !deps.resolveModelId) return;
    const id = await deps.resolveModelId(path);
    if (token !== fetchToken || pane !== activePane) return; // stale
    const url = id ? `model:${id}` : null;
    if (!url) throw new Error(`Model is not in the host catalog: ${path}. Add its directory to ModelRoots.`);
    if (url === loadedModelUrl) return;
    loadedModelUrl = url;
    // The model-bytes endpoint always serves BOS; the id in the url may keep
    // a source extension (.ifc), so the format cannot be inferred from it.
    pane.update({ kind: "model", url, format: "bos" });
  };

  const feedData = async () => {
    if (!shown || !activePane || !activeKind) return;
    const pane = activePane;
    const { nodeId, desc, values, state } = shown;
    try {
      if (activeKind === "inspector") {
        if (desc) pane.update({ kind: "inspect", node: desc, values, state, nodeId });
        return;
      }
      const port = deps.tableOnly ? desc?.outputs[0] : firstTableOutput(desc);
      if (!port) return;
      if (activeKind === "view3d" && shown.live?.kind !== "unsupported" && shown.live) {
        if (shown.live.kind === "invalid") return;
        const token = ++fetchToken;
        const data = shown.live.data;
        await feedModel(pane,token);
        if (token === fetchToken && pane === activePane) pane.update({kind:"view",data});
        return;
      }
      if (shown.pending) return; // Wait for autosave/evaluation, not the previous result.
      if (!hasResults(state)) return; // no result on the host yet; pane stays empty
      const token = ++fetchToken;
      const current = () => token === fetchToken && pane === activePane;
      if (activeKind === "view3d") await feedModel(pane, token);
      let data;
      try {
        data = activeKind === "view3d"
          ? await completeTable(deps.ctx, nodeId, port.name, current)
          : await deps.ctx.requestTable(nodeId, port.name);
      } catch (e) {
        // A result read that fails is about this node's data, not the app:
        // the host re-evaluated or the node went away between the evaluation
        // update and this request, and the next update re-feeds the pane.
        // Say so on the pane's header line instead of raising an error box.
        if (current()) source.append(headerLine("bof-app-pane-note", `\nNo rows to show: ${hostMessage(e)}`));
        return;
      }
      if (!data) return;
      if (!current()) return; // stale
      if (activeKind === "view3d") {
        // The pane queues an instances slice that arrives before the model
        // finishes loading, so pushing the table right after is safe.
        if (port.name === "view") pane.update({ kind: "view", data });
        else if (port.name === "boxes" || isBoxTable(data.columns))
          pane.update({ kind: "boxes", data });
        else pane.update({ kind: "instances", data });
      } else {
        pane.update({ kind: "table", data });
      }
    } catch (e) {
      deps.onError(e instanceof Error ? e.message : String(e));
    }
  };

  const activate = (kind: PaneKind) => {
    activeKind = kind;
    destroyPane();
    for (const el of tabs.children)
      el.classList.toggle("bof-app-tab-active", (el as HTMLElement).dataset.kind === kind);
    activeChartOptions = currentChartOptions();
    const pane = (deps.paneFactory ?? paneFactory)(kind, activeChartOptions);
    pane.onEvent(onPaneEvent);
    const host = root.ownerDocument.createElement("div");
    if (kind === "view3d") host.style.height = "100%";
    body.appendChild(host);
    pane.mount(host, deps.ctx);
    activePane = pane;
    void feedData();
  };

  const rebuildTabs = (kinds: PaneKind[]) => {
    tabs.textContent = "";
    // .bof-app-tabs sets display:flex, which beats the hidden attribute.
    const noChoice = deps.tableOnly === true && kinds.length < 2;
    tabs.style.display = noChoice ? "none" : "";
    if (noChoice) return;
    for (const kind of kinds) {
      const tab = root.ownerDocument.createElement("div");
      tab.className = "bof-app-tab";
      tab.dataset.kind = kind;
      tab.textContent = PANE_LABELS[kind];
      tab.addEventListener("click", () => activate(kind));
      tabs.appendChild(tab);
    }
  };

  return {
    showNode(next) {
      fetchToken++;
      const sameNode = shown?.nodeId === next?.nodeId;
      const sameRecipeModel = shown?.modelPath === next?.modelPath &&
        shown?.desc?.outputs.some(p => p.name === "view") &&
        next?.desc?.outputs.some(p => p.name === "view");
      shown = next;
      const live = next?.live;
      const error = live?.kind === "invalid" ? live.message : live?.kind === "ready" ? null : next?.state && !hasResults(next.state) ? next.state.error ?? next.state.status : null;
      // The header names the node in words, never just "nothing selected"
      // (TKT-81): "Answer: ..." while the pane follows the flow's answer,
      // "Showing ..." once an explicit request (double-click, the pin)
      // replaced it, each with its own detail/lineage line underneath.
      // Three classed spans whose text reads as one line: "Answer: Title
      // (id) · detail", then the lineage on a second line.
      const prefix = !next ? "" : next.default ? "Answer: " : "Showing ";
      const detail = error ?? (next?.pending ? "live · saving…" : "live graph output");
      source.replaceChildren(...(!next ? [] : [
        headerLine("bof-app-pane-title", `${prefix}${nodeTitle(next.desc?.kind ?? "")} (${next.nodeId})`),
        headerLine(`bof-app-pane-detail${error ? " bof-app-pane-detail-error" : ""}`, ` · ${detail}`),
        ...(next.lineage ? [headerLine("bof-app-pane-lineage", `\n${next.lineage}`)] : []),
      ]));
      source.setAttribute("role", error ? "alert" : "status");
      body.style.visibility = error ? "hidden" : "";
      pinBtn.hidden = !next;
      pinBtn.setAttribute("aria-pressed", String(next?.pinned ?? false));
      pinBtn.classList.toggle("bof-app-pin-active", next?.pinned ?? false);
      backBtn.hidden = !next || next.default !== false;
      if (!next) {
        activeKind = null;
        rebuildTabs([]);
        showEmpty("Select a node to see its data.");
        return;
      }
      const kinds: PaneKind[] = deps.tableOnly ? studioPanes(next.desc) : choosePanes(next.desc);
      // Recipe branches share the loaded model and renderer. Reapply their
      // complete recipe without reloading Snowdon on every node click.
      if (!sameNode && sameRecipeModel && activeKind === "view3d" && kinds.includes("view3d")) {
        rebuildTabs(kinds);
        tabs.querySelector('[data-kind="view3d"]')?.classList.add("bof-app-tab-active");
        void feedData();
        return;
      }
      if (sameNode && activeKind && kinds.includes(activeKind)) {
        // Chart options are baked in at pane creation; a param edit that
        // changes them needs a fresh pane, not just fresh data.
        if (
          activeKind === "chart" &&
          activeChartOptions &&
          !sameChartOptions(currentChartOptions(), activeChartOptions)
        ) {
          activate("chart");
          return;
        }
        void feedData();
        return;
      }
      rebuildTabs(kinds);
      activate(kinds[0]!);
    },
    refreshData() {
      void feedData();
    },
    updateSelection(ids) {
      activePane?.update({ kind: "selection", ids });
    },
    dispose() {
      destroyPane();
      root.textContent = "";
    },
  };
}
