// @vitest-environment node
// TKT-125: no text the canvas paints on a card runs past the card's edges.
// Every committed sample graph, and one graph holding every kind in the
// committed node catalog, is rendered headless in every node style by a
// recording painter, three ways: as the page shows it, with every card
// pressed to its least width, and with long values in every text param.
// Each painted label is measured against the card its anchor lies on.
//
// The recording painter measures text 10% wider than nodeSize's estimate
// (0.6 against 0.55 px per px of font size), as real fonts can be, and adds
// 8% more for the 600-weight title, which a Measure does not report; so the
// test proves the renderers fit text to the room the card has, not merely
// that nodeSize made the card wide enough.

import { afterAll, describe, expect, it } from "vitest";
// @ts-expect-error: jsdom has no type declarations in this workspace.
import { JSDOM } from "jsdom";
import { NullPainter, Runtime, type Color, type LabelOpts, type Painter, type Vec } from "gratify";
import type { NodeDescriptor } from "@bimopenflow/contracts";
import { initialState, reduce, type Action, type GraphDocument } from "@bimopenflow/state";
import { canvasView } from "../src/canvasParts.js";
import type { CanvasIntent } from "../src/canvasIntents.js";
import { createCanvasInstance } from "../src/instance.js";
import { minNodeWidth } from "../src/nodeSize.js";
import { defaultNodeStyle, nodeStyleNames, setNodeStyle } from "../src/nodeStyle.js";
import { buildCanvasModel, NOTE_KIND, type CanvasModel } from "../src/viewModel.js";
import { committedCatalog, sampleGraphs, shownModel } from "../scripts/sampleGraphs.js";

const REAL_FONT = 0.6;
const BOLD = 1.08;
/** How far below a card its footer description strip reaches (nodeRender). */
const FOOTER_REACH = 22;

interface Painted {
  readonly text: string;
  readonly left: number;
  readonly right: number;
  readonly at: Vec;
}

/** A NullPainter that records every world-layer label with its extent. */
function recordingPainter(): { painter: Painter; labels: Painted[] } {
  const labels: Painted[] = [];
  const painter: Painter = new NullPainter();
  let world = true;
  painter.measure = { text: (s, size = 13) => ({ x: s.length * size * REAL_FONT, y: size * 1.3 }) };
  painter.view = () => { world = true; };
  painter.screen = () => { world = false; };
  painter.label = (s: string, at: Vec, _c: Color, o?: LabelOpts) => {
    if (!world || s === "") return;
    const w = painter.measure.text(s, o?.size ?? 13).x * ((o?.weight ?? 400) >= 600 ? BOLD : 1);
    const align = o?.align ?? "center";
    const left = align === "left" ? at.x : align === "right" ? at.x - w : at.x - w / 2;
    labels.push({ text: s, left, right: left + w, at });
  };
  return { painter, labels };
}

// The node environment, since sampleGraphs resolves the repository from a
// file URL; the islands (column selects, inputs) get a jsdom document, and
// columnSelect.ts builds its options with the global Option constructor.
const dom = new JSDOM() as { window: Window & typeof globalThis };
Object.assign(globalThis, { Option: dom.window.Option });
const instance = createCanvasInstance({ document: dom.window.document });

/** Every label of `model` that starts or ends outside the card it is drawn on. */
function overflows(model: CanvasModel): string[] {
  const { painter, labels } = recordingPainter();
  const runtime = new Runtime<CanvasModel, CanvasIntent>(
    null,
    { init: model, update: (doc) => doc, view: (doc) => canvasView(doc, instance) },
    { headless: true, width: 1600, height: 1200 },
  );
  runtime.painter = painter;
  runtime.step(2, 1 / 60);
  labels.length = 0;
  runtime.step(1, 1 / 60);
  runtime.stop();
  return labels.flatMap((label) => {
    const card = model.nodes.find((n) =>
      label.at.x >= n.x - 1 && label.at.x <= n.x + n.w + 1 && label.at.y >= n.y && label.at.y <= n.y + n.h + FOOTER_REACH);
    if (!card) return [];
    const over = Math.max(card.x - label.left, label.right - (card.x + card.w));
    return over > 0.5 ? [`${card.id} (${card.kind}, w ${card.w}): "${label.text}" over by ${over.toFixed(1)}`] : [];
  });
}

/** Every card with a status and a long badge, so badges and chips are drawn. */
const withBadges = (model: CanvasModel): CanvasModel => ({
  ...model,
  nodes: model.nodes.map((n) => n.kind === NOTE_KIND ? n : {
    ...n,
    status: "Unready",
    badge: { status: "Unready", text: "Waiting on upstream_node_with_a_rather_long_name" },
  }),
});

/** Every card pressed to its least width, as a user may resize it. */
const narrowest = (model: CanvasModel): CanvasModel => ({
  ...model,
  nodes: model.nodes.map((n) => ({ ...n, w: minNodeWidth(n) })),
});

const LONG_WORD = "C:/data/an/unbroken/path/that/is/much/wider/than/any/card/can/be/drawn/at/all.parquet";
const LONG_TEXT = `${"a sentence with ordinary words ".repeat(12)}${LONG_WORD}`;

/** One card per kind in `catalog`, far enough apart that no two touch, plus
 *  a card whose params use the slider, range, and toggle controls. */
function catalogDocument(catalog: ReadonlyMap<string, NodeDescriptor>, long: boolean): GraphDocument {
  const actions: Action[] = [];
  [...catalog.values()].forEach((desc, i) => {
    const id = `${desc.kind.replace(/\W/g, "_")}_node_with_a_long_identifier`;
    actions.push({ type: "addNode", id, kind: desc.kind, version: desc.version });
    actions.push({ type: "setLayout", nodeId: id, layout: { x: (i % 8) * 1000, y: Math.floor(i / 8) * 1200 } });
    if (!long) return;
    for (const p of desc.params) {
      const value = p.kind === "Enum"
        ? [...(p.enumValues ?? [])].sort((a, b) => b.length - a.length)[0]
        : ["Text", "FilePath", "Expression", "Json"].includes(p.kind) ? LONG_TEXT : undefined;
      if (value !== undefined) actions.push({ type: "setParam", nodeId: id, name: p.name, value });
    }
  });
  return actions.reduce((s, a) => reduce(s, a), initialState).document;
}

const widgets: NodeDescriptor = {
  kind: "test.widgets_with_a_long_kind_name",
  version: 1,
  capability: "Pure",
  inputs: [{ name: "an_input_port_with_a_long_name", type: "Table", optional: false }],
  outputs: [{ name: "an_output_port_with_a_long_name", type: "Table", optional: false }],
  params: [
    { name: "opacityOfTheSectionFillBehindTheCut", kind: "Fraction", default: "0.5", control: { kind: "slider" } },
    { name: "elevationBandBetweenTwoLevels", kind: "Json", default: "[0.25,0.75]", control: { kind: "range", min: 0, max: 1 } },
    { name: "showEveryHiddenCategoryInTheModel", kind: "Boolean", default: "true" },
    { name: "numberOfRowsKeptAfterTheSort", kind: "Integer", default: "10" },
  ],
  description: "A card that holds each painted control, with long names.",
};

const catalog = new Map([...committedCatalog(), [widgets.kind, widgets]]);
const catalogModel = (long: boolean) => buildCanvasModel({ ...initialState, document: catalogDocument(catalog, long) }, catalog, null);

afterAll(() => setNodeStyle(defaultNodeStyle));

describe("no text overflows its card", () => {
  for (const style of nodeStyleNames) {
    it(`in the ${style} style, for every sample graph as shown and at least width`, () => {
      setNodeStyle(style);
      const report = sampleGraphs().flatMap((sample) => {
        const model = withBadges(shownModel(sample, committedCatalog()));
        return [...overflows(model), ...overflows(narrowest(model))].map((line) => `${sample.name}: ${line}`);
      });
      expect(report).toEqual([]);
    });

    it(`in the ${style} style, for every catalog kind with default and long values`, () => {
      setNodeStyle(style);
      const report = [false, true].flatMap((long) => {
        const model = withBadges(catalogModel(long));
        return [...overflows(model), ...overflows(narrowest(model))];
      });
      expect(report).toEqual([]);
    });
  }
});
