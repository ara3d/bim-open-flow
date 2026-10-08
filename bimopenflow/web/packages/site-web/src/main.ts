// The live graphs of the GitHub Pages page (site/index.html): a picker with one
// tab per graph in data/buildings.json, and the editor (@bimopenflow/app) over
// the static API, so a visitor pans, zooms, selects a node for its table or
// chart, and hovers a port for the rows on its wire. Nothing evaluates here.
import { createApp, type App } from "@bimopenflow/app";
import { watchHost } from "@bimopenflow/client/host";
import { nodeCatalog } from "./catalog";
import { RESULTS_NOTE, StaticApiClient, staticFetch, type SiteData, type SiteGraph } from "./staticApi";

const root = document.getElementById("buildings")!;

function el<K extends keyof HTMLElementTagNameMap>(tag: K, className?: string, text?: string): HTMLElementTagNameMap[K] {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
}

function mount(data: SiteData): void {
  const tabs = el("div", "tabs");
  tabs.setAttribute("role", "tablist");
  tabs.setAttribute("aria-label", "Graphs");
  const question = el("p", "question");
  const note = el("p", "hint dim", RESULTS_NOTE);
  const editor = el("div", "live-editor");
  root.replaceChildren(tabs, question, note, editor);

  // Every call reports into one host status, which the static fetch keeps
  // "connected"; /api/ask is refused, so the editor mounts no Ask panel.
  const { api, host } = watchHost((fetchFn) => new StaticApiClient({ baseUrl: "", fetch: fetchFn }),
    { fetch: staticFetch(data, nodeCatalog) });

  const buttons = data.graphs.map((graph) => {
    const button = el("button");
    button.type = "button";
    button.setAttribute("role", "tab");
    button.dataset.graph = graph.id;
    button.append(el("span", "tab-title", graph.question), el("span", "tab-building", graph.buildingTitle));
    button.addEventListener("click", () => void show(graph));
    tabs.append(button);
    return button;
  });

  let app: App | undefined;
  const select = (graph: SiteGraph) => {
    question.textContent = graph.question;
    for (const b of buttons) b.setAttribute("aria-selected", String(b.dataset.graph === graph.id));
  };
  const show = async (graph: SiteGraph) => {
    select(graph);
    await app?.openAnalysis(graph.id);
  };

  const first = data.graphs[0]!;
  select(first);
  // The page stacks the editor (index.html's .live-editor rules): the answer pane
  // above the canvas at full width, no step list. A single click shows a node,
  // since nobody edits here.
  app = createApp(editor, api, { host, initialAnalysis: first.id, heading: "BIM Open Flow", clickShows: true });
}

try {
  const response = await fetch("data/buildings.json");
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  mount(await response.json() as SiteData);
} catch (err) {
  root.textContent = `data/buildings.json did not load (${err instanceof Error ? err.message : String(err)}).`;
}
