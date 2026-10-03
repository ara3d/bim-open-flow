// The pane area: a tab strip plus one active pane for the selected node.
// Full docking is deferred by design (docs/bimopenflow-structure.md defers the
// docking/layout manager to gratify); one active pane + tabs covers the
// editor loop until then.

import {
  ensurePaneStyles,
  type ChartPaneOptions,
  type Pane,
  type PaneContext,
  type PaneEvent,
} from "@bimopenflow/panes";
import {
  chartPaneOptions,
  firstTableOutput,
  genericPanes,
  hasResults,
  paneRegistry,
  panesFor,
  studioPanes,
  type PaneRegistration,
  type ShownNode,
} from "@bimopenflow/client";
import { nodeTitle } from "@bimopenflow/graph";

export type { ShownNode };

export interface PaneAreaDeps {
  /** The DuckDB studio's pane set (see studioPanes): the table, plus a Chart
   *  tab for chart.* nodes; the tab strip shows only when there is a choice. */
  tableOnly?: boolean;
  ctx: PaneContext;
  onSelect(ids: string[]): void;
  onError(message: string): void;
  /** Catalog model id for a node's model file path; null when unknown. */
  resolveModelId?(path: string): Promise<string | null>;
  /** The panes on offer (paneRegistry); the generic panes when omitted. */
  panes?: ReadonlyMap<string, PaneRegistration>;
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
  const registry = deps.panes ?? paneRegistry(...genericPanes);
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
  let active: PaneRegistration | null = null;
  let activePane: Pane | null = null;
  let activeChartOptions: ChartPaneOptions | null = null;
  let fetchToken = 0;

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
    else if (e.action === "loadError")
      deps.onError(`${active?.label ?? "Pane"} model load failed: ${e.payload?.message ?? "unknown error"}`);
  };

  const feedData = async () => {
    if (!shown || !activePane || !active) return;
    const pane = activePane;
    const token = ++fetchToken;
    try {
      await active.feed(pane, shown, {
        ctx: deps.ctx,
        port: deps.tableOnly ? shown.desc?.outputs[0] : firstTableOutput(shown.desc),
        current: () => token === fetchToken && pane === activePane,
        resolveModelId: deps.resolveModelId,
        note: (text) => source.append(headerLine("bof-app-pane-note", `
${text}`)),
      });
    } catch (e) {
      deps.onError(e instanceof Error ? e.message : String(e));
    }
  };

  const activate = (registration: PaneRegistration) => {
    active = registration;
    destroyPane();
    for (const el of tabs.children)
      el.classList.toggle("bof-app-tab-active", (el as HTMLElement).dataset.kind === registration.kind);
    activeChartOptions = currentChartOptions();
    const pane = registration.create(activeChartOptions);
    pane.onEvent(onPaneEvent);
    const host = root.ownerDocument.createElement("div");
    if (registration.fillHeight) host.style.height = "100%";
    body.appendChild(host);
    pane.mount(host, deps.ctx);
    activePane = pane;
    void feedData();
  };

  const rebuildTabs = (kinds: readonly PaneRegistration[]) => {
    tabs.textContent = "";
    // .bof-app-tabs sets display:flex, which beats the hidden attribute.
    const noChoice = deps.tableOnly === true && kinds.length < 2;
    tabs.style.display = noChoice ? "none" : "";
    if (noChoice) return;
    for (const registration of kinds) {
      const tab = root.ownerDocument.createElement("div");
      tab.className = "bof-app-tab";
      tab.dataset.kind = registration.kind;
      tab.textContent = registration.label;
      tab.addEventListener("click", () => activate(registration));
      tabs.appendChild(tab);
    }
  };

  return {
    showNode(next) {
      fetchToken++;
      const previous = shown;
      const sameNode = previous?.nodeId === next?.nodeId;
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
        active = null;
        rebuildTabs([]);
        showEmpty("Select a node to see its data.");
        return;
      }
      const kinds = (deps.tableOnly ? studioPanes(next.desc) : panesFor(next.desc, registry))
        .flatMap((kind) => registry.get(kind) ?? []);
      const offered = active !== null && kinds.includes(active);
      // A registration may keep its mounted pane across nodes (the 3D pane
      // across recipe branches of one model) and only re-feed it.
      if (!sameNode && offered && previous && active?.keep?.(previous, next)) {
        rebuildTabs(kinds);
        tabs.querySelector(`[data-kind="${active.kind}"]`)?.classList.add("bof-app-tab-active");
        void feedData();
        return;
      }
      if (sameNode && offered) {
        // Chart options are baked in at pane creation; a param edit that
        // changes them needs a fresh pane, not just fresh data.
        if (
          active!.kind === "chart" &&
          activeChartOptions &&
          !sameChartOptions(currentChartOptions(), activeChartOptions)
        ) {
          activate(active!);
          return;
        }
        void feedData();
        return;
      }
      rebuildTabs(kinds);
      if (kinds[0]) activate(kinds[0]);
      else showEmpty("No pane is registered for this node.");
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
