// Left sidebar: the analysis list and the searchable node catalog.

import type { AnalysisSummary, NodeDescriptor } from "@bimopenflow/contracts";
import { filterCatalog } from "./catalogFilter.js";

export interface Sidebar {
  setAnalyses(list: AnalysisSummary[], activeId: string | null): void;
  setCatalog(nodes: NodeDescriptor[]): void;
}

export function createSidebar(
  root: HTMLElement,
  onOpenAnalysis: (id: string) => void,
  onAddNode: (desc: NodeDescriptor) => void,
): Sidebar {
  const doc = root.ownerDocument;
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

  const renderCatalog = () => {
    catalogList.textContent = "";
    for (const desc of filterCatalog(catalog, search.value)) {
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
