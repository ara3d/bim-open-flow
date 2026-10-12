import { describe, expect, it } from "vitest";
import { NullPainter, rect, rgb, type Painter } from "gratify";
import { nodeCardLayout, renderNodeCard, type NodeCardLayout } from "../src/nodeRender.js";
import { NODE_HEADER, type CanvasNode } from "../src/viewModel.js";

// NullPainter's measure: 0.55 * size px per character.
const measure = new NullPainter().measure;

const base: CanvasNode = {
  id: "doors",
  kind: "k.filter",
  x: 0,
  y: 0,
  w: 184,
  h: 70,
  inputs: [{ name: "in", type: "Table" }],
  outputs: [{ name: "out", type: "Table" }],
  params: [],
  selected: false,
};

const described: CanvasNode = {
  ...base,
  status: "Unready",
  badge: { status: "Unready", text: "Needs setup" },
  description: "Keeps the rows whose value in one column matches a condition, dropping the others.",
};

/** Every text entry with its left and right edge, from its alignment. */
function textSpans(layout: NodeCardLayout): { text: string; left: number; right: number }[] {
  const spans: { text: string; left: number; right: number }[] = [];
  const left = (e: { text: string; x: number; size: number }) =>
    spans.push({ text: e.text, left: e.x, right: e.x + measure.text(e.text, e.size).x });
  left(layout.title);
  if (layout.subtitle) left(layout.subtitle);
  if (layout.description) left(layout.description);
  if (layout.badge) {
    const b = layout.badge;
    const w = measure.text(b.text, b.size).x;
    const x0 = b.x - w / 2;
    spans.push({ text: b.text, left: x0, right: x0 + w });
  }
  return spans;
}

describe("nodeCardLayout", () => {
  const layout = nodeCardLayout(described, measure);

  it("draws the description on header line 2 after the id, truncated to the card", () => {
    const d = layout.description!;
    expect(d.text.endsWith("…")).toBe(true);
    expect(measure.text(d.text, d.size).x).toBeLessThanOrEqual(d.maxWidth);
    expect(d.y).toBeLessThan(NODE_HEADER);
    const idRight = layout.subtitle!.x + measure.text("doors", layout.subtitle!.size).x;
    expect(d.x).toBeGreaterThan(idRight);
    expect(d.y).toBe(layout.subtitle!.y);
  });

  it("puts the id on line 2", () => {
    expect(layout.subtitle!.text).toBe("doors");
    expect(layout.subtitle!.y).toBeGreaterThan(layout.title.y);
    expect(layout.subtitle!.y).toBeLessThan(NODE_HEADER);
  });

  it("keeps every text and the chip inside the card width", () => {
    for (const span of textSpans(layout)) {
      expect(span.left, span.text).toBeGreaterThanOrEqual(0);
      expect(span.right, span.text).toBeLessThanOrEqual(described.w);
    }
    const s = layout.status!;
    expect(s.x).toBeGreaterThanOrEqual(0);
    expect(s.x + s.w).toBeLessThanOrEqual(described.w);
  });

  it("omits the description, status, and badge on a bare node", () => {
    const bare = nodeCardLayout(base, measure);
    expect(bare.description).toBeUndefined();
    expect(bare.status).toBeUndefined();
    expect(bare.badge).toBeUndefined();
    expect(bare.title.text).not.toBe("");
  });

  it("puts the chip's text inside the chip, in the header", () => {
    const chip = layout.status!;
    expect(chip.text).toBe("Needs setup");
    expect(layout.badge!.text).toBe("Needs setup");
    expect(layout.badge!.x).toBeCloseTo(chip.x + chip.w / 2);
    expect(layout.badge!.y).toBeCloseTo(chip.y + chip.h / 2);
    expect(layout.badge!.y).toBeLessThan(NODE_HEADER);
  });

  it("shows the bare status name before the first badge", () => {
    expect(nodeCardLayout({ ...base, status: "Ok" }, measure).status?.text).toBe("Ok");
  });
});

describe("renderNodeCard", () => {
  /** A NullPainter that records the text of every label() call. */
  const labelSpy = () => {
    const painter: Painter = new NullPainter();
    const labels: string[] = [];
    painter.label = (s: string) => {
      labels.push(s);
    };
    return { painter, labels };
  };
  const grey = rgb(128, 128, 128);
  const colors = { fill: grey, edge: grey, text: grey, dim: grey, socket: grey };
  const palette = {
    status: { Ok: grey, Unready: grey, EffectPending: grey, Unavailable: grey, Error: grey },
    contributing: grey,
  };

  it("draws the title, id, description, badge, and port names", () => {
    const spy = labelSpy();
    renderNodeCard({ rect: rect(100, 50, described.w, described.h), props: described }, spy.painter, colors, palette);
    const layout = nodeCardLayout(described, measure);
    expect(spy.labels).toEqual(
      expect.arrayContaining([layout.title.text, "doors", layout.description!.text, "Needs setup", "in", "out"]),
    );
  });
});
