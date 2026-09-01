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
  type Pane,
  type PaneContext,
  type PaneEvent,
} from "@bimopenflow/panes";
import { choosePanes, firstTableOutput, hasResults, type PaneKind } from "./paneChoice.js";
import { createParamsPane } from "./paramsPane.js";

const PANE_LABELS: Record<PaneKind, string> = {
  verdict: "Verdicts",
  view3d: "3D",
  table: "Table",
  chart: "Chart",
  params: "Params",
  inspector: "Inspector",
};

const paneFactory = (kind: PaneKind): Pane => {
  switch (kind) {
    case "table": return createTablePane();
    case "chart": return createChartPane({ chart: "bar" });
    case "verdict": return createVerdictPane();
    case "view3d": return createViewPane3D();
    case "params": return createParamsPane();
    case "inspector": return createInspectorPane();
  }
};

interface ShownNode {
  nodeId: string;
  desc: NodeDescriptor | undefined;
  values: Record<string, string>;
  state: NodeState | undefined;
}

export interface PaneAreaDeps {
  ctx: PaneContext;
  onSelect(ids: string[]): void;
  onSetParam(nodeId: string, name: string, value: string): void;
  onError(message: string): void;
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
  const tabs = root.ownerDocument.createElement("div");
  tabs.className = "bof-app-tabs";
  const body = root.ownerDocument.createElement("div");
  body.className = "bof-app-panebody";
  root.append(tabs, body);

  let shown: ShownNode | null = null;
  let activeKind: PaneKind | null = null;
  let activePane: Pane | null = null;
  let fetchToken = 0;

  const destroyPane = () => {
    activePane?.destroy();
    activePane = null;
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
    else if (e.action === "setParam" && shown && e.payload)
      deps.onSetParam(shown.nodeId, e.payload.name!, e.payload.value ?? "");
  };

  const feedData = async () => {
    if (!shown || !activePane || !activeKind) return;
    const pane = activePane;
    const { nodeId, desc, values, state } = shown;
    try {
      if (activeKind === "params" || activeKind === "inspector") {
        if (desc) pane.update({ kind: "inspect", node: desc, values, state });
        return;
      }
      const port = firstTableOutput(desc);
      if (!port) return;
      if (!hasResults(state)) return; // no result on the host yet; pane stays empty
      const token = ++fetchToken;
      const data = await deps.ctx.requestTable(nodeId, port.name);
      if (token !== fetchToken || pane !== activePane) return; // stale
      if (activeKind === "view3d") {
        // TODO: push a { kind: "model" } input once the host serves geometry
        // (scheme: resolveAsset("model:{id}") -> /api/models/{id}/bos, served by the host).
        pane.update({ kind: "instances", data });
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
    const pane = paneFactory(kind);
    pane.onEvent(onPaneEvent);
    const host = root.ownerDocument.createElement("div");
    body.appendChild(host);
    pane.mount(host, deps.ctx);
    activePane = pane;
    void feedData();
  };

  const rebuildTabs = (kinds: PaneKind[]) => {
    tabs.textContent = "";
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
      const sameNode = shown?.nodeId === next?.nodeId;
      shown = next;
      if (!next) {
        activeKind = null;
        rebuildTabs([]);
        showEmpty("Select a node to see its data.");
        return;
      }
      const kinds = choosePanes(next.desc);
      if (sameNode && activeKind && kinds.includes(activeKind)) {
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
