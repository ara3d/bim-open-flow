// Left sidebar: the analysis list and the searchable node catalog.

import type { AnalysisSummary, NodeDescriptor } from "@bimopenflow/contracts";
import { groupCatalog, loadExpandedPacks, saveExpandedPacks } from "./catalogFilter.js";

export interface Sidebar {
  setAnalyses(list: AnalysisSummary[], activeId: string | null): void;
  setCatalog(nodes: NodeDescriptor[]): void;
}

const SIDEBAR_TREE_STYLE_ID = "bof-app-sidebar-tree-styles";

// The tree's own layout rules, kept out of styles.ts for now; a later sweep
// may fold this into the shared stylesheet.
function ensureSidebarStyles(doc: Document): void {
  if (doc.getElementById(SIDEBAR_TREE_STYLE_ID)) return;
  const style = doc.createElement("style");
  style.id = SIDEBAR_TREE_STYLE_ID;
  style.textContent = `
    .bof-app-catalog-group { cursor: pointer; display: flex; justify-content: space-between; align-items: center; user-select: none; }
    .bof-app-catalog-group-count { opacity: 0.65; font-weight: normal; }
  `;
  doc.head.appendChild(style);
}

export function createSidebar(
  root: HTMLElement,
  onOpenAnalysis: (id: string) => void,
  onAddNode: (desc: NodeDescriptor) => void,
): Sidebar {
  const doc = root.ownerDocument;
  ensureSidebarStyles(doc);
  root.classList.add("bof-app-sidebar");

  // Each list scrolls on its own (bof-app-analyses / bof-app-catalog) so the
  // headers and the filter box stay visible when content overflows.
  const section = (title: string, listClass: string): HTMLElement => {
    const h = doc.createElement("h3");
    h.textContent = title;
    root.appendChild(h);
    const list = doc.createElement("div");
    list.className = `bof-app-list ${listClass}`;
    root.appendChild(list);
    return list;
  };

  const analysisList = section("Flows", "bof-app-analyses");

  const catalogHeader = doc.createElement("h3");
  catalogHeader.textContent = "Node catalog";
  root.appendChild(catalogHeader);
  const search = doc.createElement("input");
  search.placeholder = "Filter nodes…";
  root.appendChild(search);
  const catalogList = doc.createElement("div");
  catalogList.className = "bof-app-list bof-app-catalog";
  root.appendChild(catalogList);

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
      const name = doc.createElement("span");
      name.className = "bof-app-catalog-group-name";
      name.textContent = group.pack;
      const count = doc.createElement("span");
      count.className = "bof-app-catalog-group-count";
      count.textContent = String(group.count);
      header.appendChild(name);
      header.appendChild(count);
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
    setAnalyses(list, activeId) {
      analysisList.textContent = "";
      for (const a of list) {
        const item = doc.createElement("div");
        item.className =
          "bof-app-item" + (a.id === activeId ? " bof-app-item-active" : "");
        item.textContent = a.id;
        item.addEventListener("click", () => onOpenAnalysis(a.id));
        analysisList.appendChild(item);
      }
    },
    setCatalog(nodes) {
      catalog = nodes;
      renderCatalog();
    },
  };
}
