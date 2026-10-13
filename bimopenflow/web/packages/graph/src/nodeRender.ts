// Node card drawing (TKT-98), moved out of canvasParts.ts: a pure layout
// function that decides where each text and the status chip go, and a
// renderer that paints that layout plus the ports and the separator above
// the param slots. One style since TKT-277: the chip. TKT-98 drew four
// (classic, banner, chip, bar) behind a picker; the owner kept the chip.
//
// Fixed frame, shared with portGeometry.ts, viewModel.ts, and canvasSlots.ts:
// the header is NODE_HEADER (46) tall, port rows are PORT_SPACING apart below
// it, and the card's height is nodeSize.nodeSize. The header holds two
// lines: the title and the status chip on line 1, the id on line 2. The id
// beside the title was tried first; a 184 px card with a badge on line 1 left
// it one letter wide. The description the page's level shows (TKT-278) is a
// wrapped block at the bottom, below the param rows, in the height nodeSize
// gave the card for it; TKT-277 had fitted it onto header line 2 after the id.
//
// Every text is fitted (gratify's fitText) to the room the card actually has
// (TKT-125): the card's width comes from nodeSize.ts, which sizes it for its
// texts measured with a fixed estimate, and a saved width can be narrower
// than that. The 600-weight title is fitted to its room divided by
// BOLD_WIDEN, since a Measure reports regular-weight widths.

import { calpha, fitText, rect, v, type Color, type Measure, type Painter, type Rect } from "gratify";
import { nodeTitle } from "./graphPreview";
import { paramSockets, portY } from "./portGeometry.js";
import {
  BOLD_WIDEN, CARD_GAP as GAP, CARD_PAD as PAD, DESC_GAP, DESC_LINE_H, DESC_PAD, DESC_SIZE, descriptionLines, NODE_HEADER,
  nodeHeight, PORT_LABEL_INSET, PORT_LABEL_SIZE as PORT_SIZE, portRowsBottom,
} from "./nodeSize.js";
import type { CanvasNode } from "./viewModel.js";

/** Everything is in card-local coordinates: (0, 0) is the card's top-left.
 *  Texts are already fitted to their room with fitText. */
export interface NodeCardLayout {
  readonly title: { text: string; x: number; y: number; size: number; weight: number };
  /** The node id. */
  readonly subtitle?: { text: string; x: number; y: number; size: number };
  /** The description block: `lines` wrapped to the card, the first centred on `y`,
   *  each next one `lineHeight` below it. */
  readonly description?: { lines: readonly string[]; x: number; y: number; size: number; lineHeight: number };
  /** The status chip at the top right, holding `text`. */
  readonly status?: { x: number; y: number; w: number; h: number; text: string };
  /** The chip's text, centred in it. */
  readonly badge?: { text: string; x: number; y: number; size: number };
  /** The writer glyph (an arrow into a tray) at the right end of header line
   *  2, on a card whose kind writes on Run: `x` is its left edge, `y` the
   *  line's baseline centre. */
  readonly effect?: { x: number; y: number; size: number };
}

const SOCKET_RADIUS = 4.5;
const BADGE_SIZE = 10;
/** The header: a 15 px title on line 1, the 11 px id on line 2. */
const COMPACT_TITLE = { size: 15, y: 14 };
const COMPACT_ID_SIZE = 11;
const COMPACT_LINE2_Y = 33;
/** Share of the card width the status text may take. */
const BADGE_SHARE = 0.45;
/** The writer glyph's box, square, on header line 2. */
const EFFECT_SIZE = 11;

/** Bottom of the port rows, relative to the card top. */
export const portsBottom = (p: { inputs: readonly unknown[]; outputs: readonly unknown[] }): number =>
  portRowsBottom(p.inputs.length, p.outputs.length);

type Title = NodeCardLayout["title"];
type Subtitle = NonNullable<NodeCardLayout["subtitle"]>;

/** `text` in the 600-weight title font, fitted to `room`. */
const fitTitle = (m: Measure, text: string, room: number, size: number): string =>
  fitText(m, text, room / BOLD_WIDEN, size);

/** Title on line 1 ending before `right`; the id on line 2, ending before
 *  the writer glyph when the card has one. */
function compactHeader(p: CanvasNode, m: Measure, right: number): Pick<NodeCardLayout, "title" | "subtitle" | "effect"> {
  const title: Title = {
    text: fitTitle(m, nodeTitle(p.kind), right - PAD, COMPACT_TITLE.size),
    x: PAD,
    y: COMPACT_TITLE.y,
    size: COMPACT_TITLE.size,
    weight: 600,
  };
  const effect = p.effect ? { x: p.w - PAD - EFFECT_SIZE, y: COMPACT_LINE2_Y, size: EFFECT_SIZE } : undefined;
  const idRoom = (effect ? effect.x - GAP : p.w - PAD) - PAD;
  const subtitle: Subtitle = { text: fitText(m, p.id, idRoom, COMPACT_ID_SIZE), x: PAD, y: COMPACT_LINE2_Y, size: COMPACT_ID_SIZE };
  return { title, subtitle, ...(effect ? { effect } : {}) };
}

/** The description wrapped to the card below its param rows, in as many
 *  lines as the card's height holds. nodeSize gave it the lines the size
 *  estimate wraps to; a wider real font that needs more ends the last line
 *  with an ellipsis rather than running past the bottom edge. */
function descriptionBlock(p: CanvasNode, m: Measure): Pick<NodeCardLayout, "description"> {
  if (!p.description) return {};
  const top = nodeHeight(p.inputs.length, p.outputs.length, p.params) + DESC_GAP;
  const room = Math.floor((p.h - top - DESC_PAD) / DESC_LINE_H);
  if (room < 1) return {};
  const lines = descriptionLines(p.description, p.w, m, room);
  return { description: { lines, x: PAD, y: top + DESC_LINE_H / 2, size: DESC_SIZE, lineHeight: DESC_LINE_H } };
}

/** Where each text and the status chip go for `props`. Pure: `measure`
 *  sizes the texts, so a test can pass a fixed one. The chip holds the
 *  badge, or the bare status name before the first badge arrives. */
export function nodeCardLayout(props: CanvasNode, measure: Measure): NodeCardLayout {
  const p = props;
  const m = measure;
  if (!p.status) return { ...compactHeader(p, m, p.w - PAD), ...descriptionBlock(p, m) };
  const text = fitText(m, p.badge?.text ?? p.status, p.w * BADGE_SHARE - 2 * GAP, BADGE_SIZE);
  const w = m.text(text, BADGE_SIZE).x + 2 * GAP;
  const chip = { x: p.w - PAD - w, y: 6, w, h: 16, text };
  return {
    ...compactHeader(p, m, chip.x - GAP),
    ...descriptionBlock(p, m),
    status: chip,
    badge: { text, x: chip.x + w / 2, y: chip.y + chip.h / 2, size: BADGE_SIZE },
  };
}

/** A box in card-local coordinates. */
export interface CardBox {
  readonly x: number;
  readonly y: number;
  readonly w: number;
  readonly h: number;
}

/** The box `props` paints: the card, its description block included, and the
 *  port sockets half outside its left and right edges. What a layout must
 *  keep clear between cards. */
export function nodeFootprint(props: CanvasNode): CardBox {
  const left = props.inputs.length > 0 ? SOCKET_RADIUS : 0;
  const right = props.outputs.length > 0 ? SOCKET_RADIUS : 0;
  return { x: 0 - left, y: 0, w: props.w + left + right, h: props.h };
}

/** How much of `room` two texts `a` and `b` wide on one line each get: both
 *  whole when they fit; else the shorter one whole if it is under half, and
 *  the longer one the rest; else half each. */
export function shareRoom(a: number, b: number, room: number): [number, number] {
  if (a + b <= room) return [a, b];
  if (a <= room / 2) return [a, room - a];
  if (b <= room / 2) return [room - b, b];
  return [room / 2, room / 2];
}

/** Each port row's input and output label, fitted to the card's width: the
 *  two labels of a row share it (shareRoom), a label alone takes it all. */
export function portLabels(p: CanvasNode, m: Measure): { inputs: string[]; outputs: string[] } {
  const w = (s: string | undefined) => (s === undefined ? 0 : m.text(s, PORT_SIZE).x);
  const rows = Math.max(p.inputs.length, p.outputs.length);
  const inputs: string[] = [];
  const outputs: string[] = [];
  for (let i = 0; i < rows; i++) {
    const a = p.inputs[i]?.name;
    const b = p.outputs[i]?.name;
    const sides = (a === undefined ? PAD : PORT_LABEL_INSET) + (b === undefined ? PAD : PORT_LABEL_INSET);
    const room = p.w - sides - (a !== undefined && b !== undefined ? 2 * GAP : 0);
    const [ra, rb] = shareRoom(w(a), w(b), room);
    if (a !== undefined) inputs.push(fitText(m, a, ra, PORT_SIZE));
    if (b !== undefined) outputs.push(fitText(m, b, rb, PORT_SIZE));
  }
  return { inputs, outputs };
}

/** Card colours the part's style computes from hover, drag, and selection. */
export interface NodeCardColors {
  fill: Color;
  edge: Color;
  text: Color;
  dim: Color;
  socket: Color;
}

/** Theme colours that do not depend on hover: canvasColors().status and the
 *  contributing highlight, passed in so this module reads no theme state. */
export interface NodeCardPalette {
  readonly status: Readonly<Record<NonNullable<CanvasNode["status"]>, Color>>;
  readonly contributing: Color;
  /** The theme's hue for this card's pack role, when it has one. The part's
   *  style already mixed it into `colors.edge`; here it means the upstream
   *  path shows by border width alone, keeping the role hue, instead of
   *  turning every card on the path the contributing blue. */
  readonly packAccent?: Color;
}

/** Width of a card's border: 2 on the path that feeds the previewed node, else 1.2. */
export const cardEdgeWidth = (p: Pick<CanvasNode, "contributing">): number => (p.contributing ? 2 : 1.2);

/** The writer glyph: an arrow pointing down into a tray, in the amber of the
 *  EffectPending status, so "writes on Run" reads the same on the card and in
 *  the chip once the graph has a pending writer. `x` is the left edge, `y` the
 *  vertical centre, `s` the square's side. */
export function writerGlyph(painter: Painter, x: number, y: number, s: number, color: Color): void {
  const cx = x + s / 2;
  const top = y - s / 2;
  const lw = 1.4;
  painter.line(v(cx, top), v(cx, top + s * 0.55), color, lw);
  painter.poly([v(cx - s * 0.28, top + s * 0.38), v(cx + s * 0.28, top + s * 0.38), v(cx, top + s * 0.68)], color);
  painter.line(v(x, top + s * 0.72), v(x, top + s), color, lw);
  painter.line(v(x, top + s), v(x + s, top + s), color, lw);
  painter.line(v(x + s, top + s), v(x + s, top + s * 0.72), color, lw);
}

/** Draws the card body (box, texts, status chip, ports, separator) for `node`. */
export function renderNodeCard(
  node: { readonly rect: Rect; readonly props: CanvasNode },
  painter: Painter,
  colors: NodeCardColors,
  palette: NodeCardPalette,
): void {
  const r = node.rect;
  const p = node.props;
  const layout = nodeCardLayout(p, painter.measure);
  const statusColor = p.status ? palette.status[p.status] : colors.dim;

  painter.box(r, 8, colors.fill, p.contributing && !palette.packAccent ? palette.contributing : colors.edge, cardEdgeWidth(p));
  if (layout.status) {
    const s = layout.status;
    painter.box(rect(r.x + s.x, r.y + s.y, s.w, s.h), s.h / 2, calpha(statusColor, 0.16), statusColor, 1);
  }

  const t = layout.title;
  painter.label(t.text, v(r.x + t.x, r.y + t.y), colors.text, { align: "left", weight: t.weight, size: t.size });
  const s = layout.subtitle;
  if (s) painter.label(s.text, v(r.x + s.x, r.y + s.y), colors.dim, { align: "left", size: s.size });
  const d = layout.description;
  d?.lines.forEach((line, i) =>
    painter.label(line, v(r.x + d.x, r.y + d.y + i * d.lineHeight), colors.dim, { align: "left", size: d.size }));
  if (layout.badge) {
    // Badge text (TKT-10): the state's own hint, or the upstream node responsible for it.
    const b = layout.badge;
    painter.label(b.text, v(r.x + b.x, r.y + b.y), statusColor, { align: "center", size: b.size });
  }
  if (layout.effect) writerGlyph(painter, r.x + layout.effect.x, r.y + layout.effect.y, layout.effect.size, palette.status.EffectPending);

  const labels = portLabels(p, painter.measure);
  p.inputs.forEach((_port, i) => {
    const y = portY(r.y, i);
    painter.dot(v(r.x, y), SOCKET_RADIUS, colors.socket);
    painter.label(labels.inputs[i]!, v(r.x + PORT_LABEL_INSET, y), colors.dim, { align: "left", size: PORT_SIZE });
  });
  p.outputs.forEach((_port, i) => {
    const y = portY(r.y, i);
    painter.dot(v(r.right, y), SOCKET_RADIUS, colors.socket);
    painter.label(labels.outputs[i]!, v(r.right - PORT_LABEL_INSET, y), colors.dim, { align: "right", size: PORT_SIZE });
  });
  // A bindable parameter's socket, on the left edge beside its row.
  for (const socket of paramSockets(p)) painter.dot(v(r.x, r.y + socket.dy), SOCKET_RADIUS, colors.socket);
  // A quiet separator between the port rows and the inline param slots.
  if (p.params.length > 0) {
    const y = r.y + portsBottom(p) + 3;
    painter.line(v(r.x + 10, y), v(r.right - 10, y), calpha(colors.dim, 0.35), 1);
  }
}
