// Left sidebar: two tabs, the open flow's steps and the searchable node
// catalog, each with the sidebar's full height. Switching flows is the top
// bar's picker (topbar.ts), so the sidebar lists no flows (TKT-116).

import type { NodeDescriptor } from "@bimopenflow/contracts";
import { groupCatalog, loadExpandedPacks, saveExpandedPacks } from "./catalogFilter.js";
import { readPref, writePref } from "./prefs.js";

export type SidebarTab = "steps" | "nodes";

export interface Sidebar {
  setCatalog(nodes: NodeDescriptor[]): void;
  /** Called when a flow opens: with no tab remembered, a flow with nodes
   *  shows Steps and an empty one shows Nodes. A remembered tab stays. */
  flowOpened(hasNodes: boolean): void;
  /** Shows a tab without remembering it (the graph demo's Nodes button). */
  showTab(tab: SidebarTab): void;
  /** Empty host for the open flow's step list (stepList.ts's createStepList). */
  readonly stepsEl: HTMLElement;
}

const TAB_PREF_KEY = "bof-app-sidebar-tab";
const TAB_LABELS: Record<SidebarTab, string> = { steps: "Steps", nodes: "Nodes" };

const isTab = (value: string | null): value is SidebarTab => value === "steps" || value === "nodes";

/** The tab to show when a flow opens: the remembered one, else Steps for a
 *  flow with nodes and Nodes for an empty flow. */
export function tabForFlow(remembered: string | null, hasNodes: boolean): SidebarTab {
  if (isTab(remembered)) return remembered;
  return hasNodes ? "steps" : "nodes";
}

export function createSidebar(
  root: HTMLElement,
  onAddNode: (desc: NodeDescriptor) => void,
): Sidebar {
  const doc = root.ownerDocument;
  root.classList.add("bof-app-sidebar");

  // The tab strip reuses the pane area's look (styles.ts, bof-app-tabs).
  const tabStrip = doc.createElement("div");
  tabStrip.className = "bof-app-tabs";
  tabStrip.setAttribute("role", "tablist");
  root.appendChild(tabStrip);

  const panel = (tab: SidebarTab): HTMLElement => {
    const el = doc.createElement("div");
    el.className = `bof-app-sidebar-panel bof-app-sidebar-panel-${tab}`;
    el.setAttribute("role", "tabpanel");
    el.setAttribute("aria-label", TAB_LABELS[tab]);
    root.appendChild(el);
    return el;
  };
  const panels: Record<SidebarTab, HTMLElement> = { steps: panel("steps"), nodes: panel("nodes") };

  const tabs = (Object.keys(TAB_LABELS) as SidebarTab[]).map((tab) => {
    const el = doc.createElement("button");
    el.type = "button";
    el.className = "bof-app-tab";
    el.dataset.tab = tab;
    el.setAttribute("role", "tab");
    el.textContent = TAB_LABELS[tab];
    el.addEventListener("click", () => {
      writePref(TAB_PREF_KEY, tab);
      showTab(tab);
    });
    tabStrip.appendChild(el);
    return el;
  });

  function showTab(tab: SidebarTab): void {
    for (const el of tabs) {
      const active = el.dataset.tab === tab;
      el.classList.toggle("bof-app-tab-active", active);
      el.setAttribute("aria-selected", String(active));
    }
    for (const [name, el] of Object.entries(panels)) el.hidden = name !== tab;
  }
  showTab(tabForFlow(readPref(TAB_PREF_KEY), true));

  // Each list scrolls on its own, so the tabs and the filter box stay visible.
  const stepsEl = doc.createElement("div");
  stepsEl.className = "bof-app-list bof-app-steps";
  panels.steps.appendChild(stepsEl);

  const search = doc.createElement("input");
  search.placeholder = "Filter nodes…";
  const catalogList = doc.createElement("div");
  catalogList.className = "bof-app-list bof-app-catalog";
  panels.nodes.append(search, catalogList);

  let catalog: NodeDescriptor[] = [];
  // Packs the user has expanded by hand; persisted so a reload restores them.
  // Typing in the filter opens matching packs without touching this set, so
  // clearing the filter restores exactly what the user had open.
  const expanded = loadExpandedPacks();

  // Grouped by the kind's dotted prefix ("table.limit" -> "table") so a large
  // catalog opens collapsed and stays scannable; groups and kinds render
  // alphabetically. See catalogFilter.ts's groupCatalog for the tree logic.
  const renderCatalog = () => {
    catalogList.textContent = "";
    for (const group of groupCatalog(catalog, search.value, expanded)) {
      const header = doc.createElement("h4");
      header.className = "bof-app-catalog-group";
      header.setAttribute("aria-expanded", String(group.open));
      const triangle = doc.createElement("span");
      triangle.className = "bof-app-catalog-group-triangle";
      triangle.textContent = group.open ? "▾" : "▸";
      const name = doc.createElement("span");
      name.className = "bof-app-catalog-group-name";
      name.textContent = group.pack;
      const count = doc.createElement("span");
      count.className = "bof-app-catalog-group-count";
      count.textContent = String(group.count);
      header.append(triangle, name, count);
      header.addEventListener("click", () => {
        if (expanded.has(group.pack)) expanded.delete(group.pack);
        else expanded.add(group.pack);
        saveExpandedPacks(expanded);
        renderCatalog();
      });
      catalogList.appendChild(header);
      if (!group.open) continue;
      for (const desc of group.nodes) {
        const item = doc.createElement("div");
        item.className = "bof-app-item";
        item.textContent = desc.kind;
        item.title = desc.description;
        const detail = doc.createElement("small");
        detail.textContent = desc.description;
        item.appendChild(detail);
        item.addEventListener("click", () => onAddNode(desc));
        catalogList.appendChild(item);
      }
    }
  };
  search.addEventListener("input", renderCatalog);

  return {
    stepsEl,
    setCatalog(nodes) {
      catalog = nodes;
      renderCatalog();
    },
    flowOpened(hasNodes) {
      showTab(tabForFlow(readPref(TAB_PREF_KEY), hasNodes));
    },
    showTab,
  };
}
