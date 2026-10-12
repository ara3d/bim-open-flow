// @vitest-environment jsdom
// TKT-217: the path box's "use <source>" offer never names the node the box
// sits on, and a lit card (a hovered chip's source and users) is drawn so it
// shows at a glance.

import { afterEach, describe, expect, it } from "vitest";
import { NullPainter, rgb, type Painter } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { createStore } from "@bimopenflow/state";
import { FilePathControls } from "../src/filePathControl.js";
import { cardEdgeWidth, LIT_EDGE_WIDTH, LIT_FILL_MIX, renderNodeCard } from "../src/nodeRender.js";
import { promotePlan } from "../src/sourcePlans.js";
import { findSourceByPath, sourceOffer } from "../src/sources.js";
import type { CanvasNode } from "../src/viewModel.js";
import { catalog as fixtureCatalog } from "./sourceFixtures.js";

afterEach(() => { document.body.replaceChildren(); });

const PATH = "C:/m/city.duckdb";

/** duck.source as the host declares it: its own path takes a Database socket. */
const duckSource: NodeDescriptor = {
  kind: "duck.source", version: 1, capability: "Pure", description: "", inputs: [],
  outputs: [{ name: "source", type: "Database", optional: false }],
  params: [{ name: "path", kind: "FilePath", default: "", bindable: "Database", suggest: { kind: "FilesUnderRoots", source: ".duckdb" } }],
};
const catalog = new Map([...fixtureCatalog, [duckSource.kind, duckSource]]);

function graph(ids: string[]) {
  const store = createStore();
  for (const id of ids) {
    store.dispatch({ type: "addNode", id, kind: "duck.source", version: 1 });
    store.dispatch({ type: "setParam", nodeId: id, name: "path", value: PATH });
  }
  store.dispatch({ type: "addNode", id: "q1", kind: "duck.query", version: 1 });
  store.dispatch({ type: "setParam", nodeId: "q1", name: "path", value: PATH });
  return store;
}

describe("the use-source offer", () => {
  it("does not name the node asking, but does name another source holding the path", () => {
    const { document: doc } = graph(["db"]).getState();
    expect(sourceOffer(doc, catalog, "db", "path", PATH)).toBeNull();
    expect(sourceOffer(doc, catalog, "q1", "path", PATH)).toEqual({ id: "db", endpoint: "db.source" });
    const two = graph(["db", "db2"]).getState().document;
    expect(sourceOffer(two, catalog, "db", "path", PATH)?.id).toBe("db2");
    expect(sourceOffer(two, catalog, "db2", "path", PATH)?.id).toBe("db");
  });

  it("is still found by path when no node is excepted, and null for a parameter with no socket", () => {
    const { document: doc } = graph(["db"]).getState();
    expect(findSourceByPath(doc, catalog, PATH, "Database")?.id).toBe("db");
    expect(findSourceByPath(doc, catalog, PATH, "Database", "db")).toBeNull();
    expect(sourceOffer(doc, catalog, "q1", "sql", PATH)).toBeNull();
  });

  it("shows no button on a source's own path box, and one on a reader's", () => {
    const store = graph(["db"]);
    const controls = new FilePathControls(
      () => Promise.resolve({ status: "Ok", values: [] }), () => {},
      { document, match: (node, param, value) => sourceOffer(store.getState().document, catalog, node, param, value) },
    );
    const own = controls.get("db", "path", PATH, ".duckdb");
    const reader = controls.get("q1", "path", PATH, ".duckdb");
    const useButton = (el: HTMLElement) => el.querySelector<HTMLButtonElement>('button[aria-label$="use source"]')!;
    expect(useButton(own).hidden).toBe(true);
    expect(useButton(reader).hidden).toBe(false);
    expect(useButton(reader).textContent).toBe("use db");
    controls.prune(new Set());
  });

  it("does not make Share as source link a source to itself", () => {
    const store = graph(["db"]);
    const plan = promotePlan(store.getState(), catalog, "db", "path");
    if ("error" in plan) throw new Error(plan.error);
    expect(plan.source.id).not.toBe("db");
    expect(plan.actions.at(-1)).toMatchObject({ type: "connect", to: "db.path" });
  });
});

describe("the lit highlight", () => {
  const card: CanvasNode = {
    id: "db", kind: "duck.source", x: 0, y: 0, w: 184, h: 70, inputs: [], outputs: [], params: [], selected: false,
  };
  const colors = { fill: rgb(1, 1, 1), edge: rgb(0.2, 0.4, 0.8), text: rgb(0, 0, 0), dim: rgb(0.4, 0.4, 0.4), socket: rgb(0, 0, 1) };
  const palette = { status: {} as never, contributing: rgb(1, 0, 0) };

  /** The border colour and width of the card's outline, the first box drawn. */
  function outline(props: CanvasNode) {
    const boxes: { stroke: unknown; width: unknown }[] = [];
    const painter = Object.create(new NullPainter()) as Painter;
    painter.box = ((_r: unknown, _radius: unknown, _fill: unknown, stroke?: unknown, width?: unknown) => { boxes.push({ stroke, width }); }) as Painter["box"];
    renderNodeCard({ rect: { x: 0, y: 0, w: props.w, h: props.h, right: props.w, bottom: props.h } as never, props }, painter, colors, palette);
    return boxes[0]!;
  }

  it("draws a lit card with a border over twice as wide as a plain one, in the accent edge", () => {
    expect(cardEdgeWidth(card)).toBe(1.2);
    expect(cardEdgeWidth({ ...card, contributing: true })).toBe(2);
    expect(cardEdgeWidth({ ...card, lit: true })).toBe(LIT_EDGE_WIDTH);
    expect(LIT_EDGE_WIDTH).toBeGreaterThanOrEqual(2.5 * 1.2);
    const lit = outline({ ...card, lit: true, contributing: true });
    expect(lit).toEqual({ stroke: colors.edge, width: LIT_EDGE_WIDTH });
    expect(outline({ ...card, contributing: true }).stroke).toBe(palette.contributing);
  });

  it("tints the fill toward the accent by a share a reader can see", () => {
    expect(LIT_FILL_MIX).toBeGreaterThanOrEqual(0.15);
    expect(LIT_FILL_MIX).toBeLessThanOrEqual(0.4);
  });
});
