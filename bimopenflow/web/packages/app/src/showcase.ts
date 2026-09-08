import { createViewPane3D, ensurePaneStyles } from "@bimopenflow/panes";
import type { TableSlice } from "@bimopenflow/contracts";
import type { ViewStep } from "../../panes/src/viewRecipe";
import "./showcase.css";
import graphUrl from "../../../../../samples/snowdon-analyses/snowdon-toolkit.json?url";

const root = document.querySelector<HTMLDivElement>("#lab")!;
root.innerHTML = `<header><a href="/">BIM FLOW</a><span>VISUALIZATION TOOLKIT / 3D LAB</span><label class="file">Open local BOS / BFAST<input type="file" accept=".bos,.bfast" aria-label="Open local model"></label></header>
<aside><p class="eyebrow">SNOWDON TOWERS</p><h1>A building.<br>Seven ways in.</h1><p class="intro">Explore a model through composable 3D nodes. Each view uses the same pane as the graph editor.</p><nav aria-label="3D examples"></nav><div class="adjust"><label for="amount">Section position <output id="amountLabel">50%</output></label><input id="amount" type="range" min="1" max="99" value="50"></div><p class="note">Source categories drive colors. Sections are uncapped. Exploded positions are presentation offsets, not changes to the building.</p><a class="graph" href="/snowdon-toolkit.json" download>Download the example graph ↗</a></aside>
<main><div class="view-heading"><div><p class="eyebrow" id="node"></p><h2 id="title"></h2></div><span id="picked">Click geometry to inspect its entity ID</span></div><div id="viewer"></div><footer id="description"></footer></main>`;

const scene: ViewStep = { operation: "scene", input: { path: "Snowdon" } };
document.querySelector<HTMLAnchorElement>(".graph")!.href = graphUrl;
const categories: ViewStep = { operation: "categoryStyle", input: { opacity: 1 } };
const examples: readonly { title: string; node: string; description: string; steps: readonly ViewStep[] }[] = [
  { title: "Model overview", node: "view3d.scene", description: "The full Snowdon model. Orbit, pan and zoom; fit the model or save a PNG.", steps: [] },
  { title: "Category atlas", node: "view3d.categoryStyle", description: "A stable palette separates source categories. Missing categories stay gray.", steps: [categories] },
  { title: "Building cutaway", node: "view3d.section", description: "A horizontal section reveals the interior. Adjust the cut as a fraction of the original model height.", steps: [categories, { operation: "section", input: { axis: "z", fraction: .5 } }] },
  { title: "Interior section box", node: "view3d.sectionBox", description: "Six clipping planes reveal the center of the building without changing its geometry.", steps: [categories, { operation: "sectionBox", input: { fraction: .5 } }] },
  { title: "Exploded categories", node: "view3d.explode", description: "Source categories fan apart in the ground plane. Reset restores their original placement.", steps: [categories, { operation: "explode", input: { by: "category", strength: .5 } }] },
  { title: "Plan inspection", node: "view3d.projection", description: "An orthographic overhead view with a section through the building. Navigation keeps the plan orientation.", steps: [categories, { operation: "section", input: { axis: "z", fraction: .5 } }, { operation: "projection", input: { mode: "plan" } }] },
  { title: "Ghosted presentation", node: "view3d.environment", description: "Translucent category colors against a dark review background. Transparency is approximate; overlapping surfaces can show sorting artifacts.", steps: [{ operation: "categoryStyle", input: { opacity: .22 } }, { operation: "environment", input: { theme: "dark", grid: true } }] },
];
ensurePaneStyles(document);
const pane = createViewPane3D();
const container = document.querySelector<HTMLDivElement>("#viewer")!;
pane.mount(container, { resolveAsset: url => url, requestTable: async () => { throw new Error("This example uses view recipes."); } });
pane.onEvent(event => {
  if (event.kind === "selection") document.querySelector("#picked")!.textContent = "Entity " + event.event.ids.join(", ");
});
let active = 0;
const amount = document.querySelector<HTMLInputElement>("#amount")!;
const apply = () => {
  const example = examples[active]!;
  const fraction = Number(amount.value) / 100;
  const steps = [scene, ...example.steps.map(step =>
    step.operation === "section" || step.operation === "sectionBox"
      ? { ...step, input: { ...step.input, fraction } }
      : step.operation === "explode" ? { ...step, input: { ...step.input, strength: fraction } } : step)];
  const data: TableSlice = { columns: [{ name: "operation", type: "Text" }, { name: "input", type: "Text" }], rows: steps.map(s => [s.operation, JSON.stringify(s.input)]), skip: 0, totalRows: steps.length };
  pane.update({ kind: "view", data });
};
examples.forEach((example, index) => {
  const button = document.createElement("button");
  button.innerHTML = `<span class="number">0${index+1}</span><span>${example.title}<small>${example.node}</small></span><span class="arrow">↗</span>`;
  button.onclick = () => {
    active = index;
    for (const [i, child] of [...document.querySelector("nav")!.children].entries()) child.setAttribute("aria-pressed", String(i === index));
    document.querySelector("#node")!.textContent = example.node;
    document.querySelector("#title")!.textContent = example.title;
    document.querySelector("#description")!.textContent = example.description;
    const adjustable = example.steps.some(s => ["section", "sectionBox", "explode"].includes(s.operation));
    document.querySelector<HTMLElement>(".adjust")!.hidden = !adjustable;
    apply();
  };
  document.querySelector("nav")!.append(button);
});
amount.addEventListener("change", apply);
amount.addEventListener("input", () => { document.querySelector("#amountLabel")!.textContent = amount.value + "%"; });
pane.update({ kind: "model", url: "/__bimflow/snowdon.bfast", format: "bfast" });
document.querySelector<HTMLButtonElement>("nav button")!.click();
let localUrl: string | undefined;
document.querySelector<HTMLInputElement>('input[type="file"]')!.onchange = event => {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  if (localUrl) URL.revokeObjectURL(localUrl);
  localUrl = URL.createObjectURL(file);
  document.querySelector(".eyebrow")!.textContent = file.name;
  pane.update({ kind: "model", url: localUrl, format: /\.bfast$/i.test(file.name) ? "bfast" : "bos" });
  apply();
};
window.addEventListener("pagehide", () => { pane.destroy(); if (localUrl) URL.revokeObjectURL(localUrl); }, { once: true });
