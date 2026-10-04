// Draws the sample graphs over public buildings from data/buildings.json, which
// the tests write (PublicBuildingsSiteData in tests/flow/BimOpenFlow.TableWorkflows.Tests)
// by evaluating samples/buildings with the generic node packs. Nothing is evaluated
// here: the page shows each graph, a bar chart of its chart.bar node, and the table on
// any node a visitor selects, all precomputed.

const CARD_W = 240;
/** Wide graphs shrink to fit the panel, but never below this scale; past it the panel scrolls. */
const MIN_SCALE = 0.6;
const HEAD_H = 46;
const LINE_H = 18;
const PARAM_MAX = 30;
const SERIES = ["#2f66ce", "#0f9d8a", "#d97706", "#c2416c"];
const SVG_NS = "http://www.w3.org/2000/svg";

const el = (tag, attrs = {}, ...children) => {
  const node = document.createElement(tag);
  for (const [k, v] of Object.entries(attrs)) {
    if (k === "class") node.className = v;
    else if (k.startsWith("on")) node.addEventListener(k.slice(2), v);
    else node.setAttribute(k, v);
  }
  for (const c of children.flat()) if (c != null) node.append(c);
  return node;
};

const svg = (tag, attrs = {}, ...children) => {
  const node = document.createElementNS(SVG_NS, tag);
  for (const [k, v] of Object.entries(attrs)) node.setAttribute(k, v);
  for (const c of children.flat()) if (c != null) node.append(c);
  return node;
};

const clip = (text, max) => (text.length > max ? text.slice(0, max - 1) + "…" : text);
const shortPath = (value) => value.replace(/^\{PUBLIC\}\//, "");
const formatCell = (v) => (v === null ? "" : typeof v === "number" ? v.toLocaleString("en-US") : String(v));

/** The graph's node list with each node's parameters, input ports, and output ports. */
function graphModel(doc) {
  const values = doc.values ?? {};
  const nodes = doc.structure.nodes.map((n) => ({
    id: n.id,
    kind: n.kind,
    pos: doc.layout?.[n.id] ?? { x: 0, y: 0 },
    params: Object.entries(values[n.id] ?? {}).filter(([k]) => !k.startsWith("descending")),
    inputs: [],
    outputs: [],
  }));
  const byId = new Map(nodes.map((n) => [n.id, n]));
  const edges = doc.structure.edges.map((e) => {
    const [fromNode, fromPort] = e.from.split(".");
    const [toNode, toPort] = e.to.split(".");
    const from = byId.get(fromNode);
    const to = byId.get(toNode);
    if (!from.outputs.includes(fromPort)) from.outputs.push(fromPort);
    if (!to.inputs.includes(toPort)) to.inputs.push(toPort);
    return { from, fromPort, to, toPort };
  });
  for (const n of nodes) n.height = HEAD_H + Math.max(1, n.params.length) * LINE_H + 12;
  return { nodes, edges };
}

const portY = (node, ports, port) => node.pos.y + HEAD_H - 8 + (ports.indexOf(port) + 1) * (LINE_H + 2) - LINE_H / 2;

function drawGraph(graph, selected, onSelect) {
  const { nodes, edges } = graphModel(graph.document);
  const pad = 24;
  const minX = Math.min(...nodes.map((n) => n.pos.x)) - pad;
  const minY = Math.min(...nodes.map((n) => n.pos.y)) - pad;
  const maxX = Math.max(...nodes.map((n) => n.pos.x + CARD_W)) + pad;
  const maxY = Math.max(...nodes.map((n) => n.pos.y + n.height)) + pad;
  const width = maxX - minX;
  const root = svg("svg", {
    viewBox: `${minX} ${minY} ${width} ${maxY - minY}`,
    style: `width:100%;min-width:${Math.round(width * MIN_SCALE)}px;max-width:${width}px;height:auto`,
    role: "group",
    "aria-label": `Graph ${graph.id}`,
  });
  for (const e of edges) {
    const x1 = e.from.pos.x + CARD_W;
    const y1 = portY(e.from, e.from.outputs, e.fromPort);
    const x2 = e.to.pos.x;
    const y2 = portY(e.to, e.to.inputs, e.toPort);
    const bend = Math.max(40, (x2 - x1) / 2);
    const rows = graph.nodes[e.from.id]?.rowCount;
    root.append(svg("path", { class: "wire", d: `M${x1},${y1} C${x1 + bend},${y1} ${x2 - bend},${y2} ${x2},${y2}` }));
    if (rows !== undefined) {
      const label = svg("text", { class: "wire-label", x: String((x1 + x2) / 2), y: String((y1 + y2) / 2 - 6), "text-anchor": "middle" }, rows.toLocaleString("en-US"));
      label.append(svg("title", {}, `${rows.toLocaleString("en-US")} rows from ${e.from.id}`));
      root.append(label);
    }
  }
  for (const n of nodes) {
    const result = graph.nodes[n.id] ?? { status: "Unknown" };
    const card = svg("g", {
      class: `card${n.id === selected ? " selected" : ""}`,
      tabindex: "0",
      role: "button",
      "aria-label": `${n.id} (${n.kind}), ${result.status}${result.rowCount !== undefined ? `, ${result.rowCount} rows` : ""}`,
    });
    card.addEventListener("click", () => onSelect(n.id));
    card.addEventListener("keydown", (ev) => {
      if (ev.key === "Enter" || ev.key === " ") {
        ev.preventDefault();
        onSelect(n.id);
      }
    });
    card.append(
      svg("rect", { class: "card-box", x: String(n.pos.x), y: String(n.pos.y), width: String(CARD_W), height: String(n.height), rx: "8" }),
      svg("rect", { class: "card-head", x: String(n.pos.x), y: String(n.pos.y), width: String(CARD_W), height: String(HEAD_H - 8), rx: "8" }),
      svg("circle", { class: `dot dot-${result.status}`, cx: String(n.pos.x + CARD_W - 16), cy: String(n.pos.y + 19), r: "5" }),
      svg("text", { class: "card-kind", x: String(n.pos.x + 12), y: String(n.pos.y + 17) }, n.kind),
      svg("text", { class: "card-id", x: String(n.pos.x + 12), y: String(n.pos.y + 32) }, n.id),
    );
    n.params.forEach(([k, v], i) => {
      const text = `${k} = ${clip(shortPath(String(v)), PARAM_MAX - k.length)}`;
      const t = svg("text", { class: "card-param", x: String(n.pos.x + 12), y: String(n.pos.y + HEAD_H + 8 + i * LINE_H) }, text);
      t.append(svg("title", {}, `${k} = ${v}`));
      card.append(t);
    });
    for (const p of n.inputs) card.append(svg("circle", { class: "port", cx: String(n.pos.x), cy: String(portY(n, n.inputs, p)), r: "4" }));
    for (const p of n.outputs) card.append(svg("circle", { class: "port", cx: String(n.pos.x + CARD_W), cy: String(portY(n, n.outputs, p)), r: "4" }));
    root.append(card);
  }
  return root;
}

function drawChart(graph) {
  const chartNode = graph.document.structure.nodes.find((n) => n.kind === "chart.bar");
  if (!chartNode) return null;
  const params = graph.document.values[chartNode.id];
  const result = graph.nodes[chartNode.id];
  const names = result.columns.map((c) => c.name);
  const labelIx = names.indexOf(params.labelColumn);
  const series = params.valueColumns.split(",").map((s) => s.trim()).filter(Boolean);
  const seriesIx = series.map((s) => names.indexOf(s));
  const max = Math.max(1, ...result.rows.flatMap((r) => seriesIx.map((i) => r[i] ?? 0)));
  const legend = series.length > 1
    ? el("ul", { class: "legend" }, series.map((s, i) => el("li", {}, el("span", { class: "swatch", style: `background:${SERIES[i % SERIES.length]}` }), s)))
    : null;
  const rows = result.rows.map((r) =>
    el("div", { class: "bar-row" },
      el("div", { class: "bar-label", title: formatCell(r[labelIx]) }, formatCell(r[labelIx])),
      el("div", { class: "bars" },
        seriesIx.map((ix, i) => {
          const v = r[ix];
          return el("div", { class: "bar-line" },
            el("span", { class: "bar", style: `width:${((v ?? 0) / max) * 100}%;background:${SERIES[i % SERIES.length]}` }),
            el("span", { class: "bar-value" }, v === null ? "none" : formatCell(v)));
        }))));
  const more = result.rowCount > result.rows.length ? el("p", { class: "dim" }, `First ${result.rows.length} of ${result.rowCount} rows.`) : null;
  return el("figure", { class: "chart" },
    el("figcaption", {}, params.title || chartNode.id, el("span", { class: "dim" }, ` · node ${chartNode.id}`)),
    legend, rows, more);
}

function drawTable(graph, nodeId) {
  const node = graph.document.structure.nodes.find((n) => n.id === nodeId);
  const result = graph.nodes[nodeId];
  const head = el("p", { class: "table-head" },
    el("strong", {}, nodeId), ` (${node.kind}) · ${result.status}`,
    result.rowCount !== undefined ? ` · ${result.rowCount.toLocaleString("en-US")} rows, ${result.columns.length} columns` : "");
  if (!result.columns) return el("div", {}, head, el("p", { class: "dim" }, result.error ?? "No table output."));
  const table = el("table", {},
    el("thead", {}, el("tr", {}, result.columns.map((c) => el("th", { title: c.type }, c.name)))),
    el("tbody", {}, result.rows.map((r) => el("tr", {}, r.map((v) => el("td", { class: typeof v === "number" ? "num" : "" }, formatCell(v)))))));
  const shown = result.rowCount > result.rows.length
    ? el("p", { class: "dim" }, `Showing the first ${result.rows.length} of ${result.rowCount.toLocaleString("en-US")} rows.`)
    : null;
  return el("div", {}, head, el("div", { class: "table-wrap" }, table), shown);
}

function lastNode(graph) {
  const chart = graph.document.structure.nodes.find((n) => n.kind === "chart.bar");
  return chart ? chart.id : graph.document.structure.nodes[0].id;
}

async function main() {
  const host = document.getElementById("buildings");
  let data;
  try {
    const response = await fetch("data/buildings.json");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    data = await response.json();
  } catch (err) {
    host.append(el("p", { class: "dim" }, `The precomputed results did not load (${err.message}).`));
    return;
  }
  const tabs = el("div", { class: "tabs", role: "tablist", "aria-label": "Sample graphs" });
  const panel = el("div", { class: "panel", role: "tabpanel" });
  host.append(tabs, panel);
  let current = null;

  const show = (graph, selected) => {
    current = graph;
    for (const b of tabs.querySelectorAll("button")) b.setAttribute("aria-selected", String(b.dataset.id === graph.id));
    const graphWrap = el("div", { class: "graph-wrap" }, drawGraph(graph, selected, (id) => show(graph, id)));
    panel.replaceChildren(
      el("p", { class: "question" }, graph.question),
      el("p", { class: "dim" }, `${graph.buildingTitle} · `, el("code", {}, `samples/buildings/${graph.id}.json`), ` over `, el("code", {}, `${graph.building}.duckdb`)),
      el("div", { class: "graph-and-chart" }, graphWrap),
      el("p", { class: "dim hint" }, "Select a node to see the table it outputs. The number on each wire is the count of rows that travel on it."),
      el("div", { class: "result" }, drawChart(graph), drawTable(graph, selected)),
    );
  };

  for (const graph of data.graphs) {
    tabs.append(el("button", { type: "button", role: "tab", "data-id": graph.id, onclick: () => show(graph, lastNode(graph)) },
      el("span", { class: "tab-building" }, graph.buildingTitle.split(",")[0]),
      el("span", {}, graph.id.replace(/^[a-z]+-/, "").replaceAll("-", " "))));
  }
  show(data.graphs[0], lastNode(data.graphs[0]));
  window.__buildingsReady = { graphs: data.graphs.length, current: () => current?.id };
}

main();
