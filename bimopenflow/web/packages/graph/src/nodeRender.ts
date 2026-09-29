// Node card drawing (TKT-98), moved out of canvasParts.ts: a pure layout
// function that decides where each text and the status indicator go in each
// of the four styles (nodeStyle.ts), and a renderer that paints that layout
// plus the ports and the separator above the param slots.
//
// Fixed frame, shared with portGeometry.ts, viewModel.ts, and canvasSlots.ts:
// the header is NODE_HEADER (46) tall, port rows are PORT_SPACING apart below
// it, and the card's height is viewModel.nodeHeight. No style changes those.
// A card has no spare room inside that frame (a param-less card ends 12 px
// under its last port centre; a card with params keeps 10 px of padding), so
// a style that keeps the two-line header of today draws the description in a
// footer strip hanging below the card's bottom edge. The strip is drawn only;
// the node's hit rect stays the card. The compact styles (banner, chip) shrink
// the title and the id so both lines of the header hold text: the title and
// the status on line 1, the id and then the description on line 2. The id
// beside the title was tried first; a 184 px card with a badge on line 1 left
// it one letter wide.

import { calpha, cmix, fitText, rect, v, type Color, type Measure, type Painter, type Rect } from "gratify";
import { nodeTitle } from "./graphPreview";
import { nodeStyleNames, type NodeStyleName } from "./nodeStyle.js";
import { portY } from "./portGeometry.js";
import { NODE_HEADER, PORT_SPACING } from "./nodeSize.js";
import type { CanvasNode } from "./viewModel.js";

/** Where the description line is drawn: on the header's second line, or in a
 *  strip hanging below the card. */
export type DescriptionPlacement = "header" | "footer";

/** Everything is in card-local coordinates: (0, 0) is the card's top-left.
 *  Texts are already fitted to their room with fitText. */
export interface NodeCardLayout {
  readonly title: { text: string; x: number; y: number; size: number; weight: number };
  /** The node id. */
  readonly subtitle?: { text: string; x: number; y: number; size: number };
  readonly description?: {
    text: string;
    x: number;
    y: number;
    size: number;
    maxWidth: number;
    placement: DescriptionPlacement;
  };
  /** The strip behind a footer description; below the card (y >= card height). */
  readonly footer?: { x: number; y: number; w: number; h: number };
  readonly status?: { kind: "dot" | "chip" | "bar" | "tint"; x: number; y: number; w: number; h: number; text?: string };
  readonly badge?: { text: string; x: number; y: number; size: number; align: "left" | "right" | "center" };
}

const PAD = 12;
const SOCKET_RADIUS = 4.5;
const PORT_SIZE = 13;
const BADGE_SIZE = 10;
const DESC_SIZE = 11;
/** The header of today: a 17 px title on the first line, the 13 px id on the second. */
const TALL_TITLE = { size: 17, y: 16 };
const TALL_ID = { size: 13, y: NODE_HEADER - 9 };
/** The compact header: a 15 px title on line 1, the 11 px id and the
 *  description after it on line 2. */
const COMPACT_TITLE = { size: 15, y: 14 };
const COMPACT_ID_SIZE = 11;
const COMPACT_LINE2_Y = 33;
/** Share of the card width the id may take on the compact line 2. */
const COMPACT_ID_SHARE = 0.4;
const GAP = 6;
const FOOTER_H = 18;
const FOOTER_GAP = 3;
/** Share of the card width the status text may take. */
const BADGE_SHARE = 0.45;

/** Bottom of the port rows, relative to the card top. */
export const portsBottom = (p: { inputs: readonly unknown[]; outputs: readonly unknown[] }): number =>
  NODE_HEADER + Math.max(p.inputs.length, p.outputs.length, 1) * PORT_SPACING;

type Title = NodeCardLayout["title"];
type Subtitle = NonNullable<NodeCardLayout["subtitle"]>;
type Description = NonNullable<NodeCardLayout["description"]>;
type Badge = NonNullable<NodeCardLayout["badge"]>;

/** Title on line 1 and the id on line 2, both left, at the sizes of today.
 *  The id stops short of a right-aligned badge of width `badgeW` on line 2. */
function tallHeader(p: CanvasNode, m: Measure, titleRoom: number, badgeW: number): { title: Title; subtitle: Subtitle } {
  const idRoom = p.w - 2 * PAD - (badgeW > 0 ? badgeW + GAP : 0);
  return {
    title: { text: fitText(m, nodeTitle(p.kind), titleRoom, TALL_TITLE.size), x: PAD, y: TALL_TITLE.y, size: TALL_TITLE.size, weight: 600 },
    subtitle: { text: fitText(m, p.id, idRoom, TALL_ID.size), x: PAD, y: TALL_ID.y, size: TALL_ID.size },
  };
}

/** Title on line 1 ending before `right`; the id and then the description
 *  on line 2. */
function compactHeader(p: CanvasNode, m: Measure, right: number): Pick<NodeCardLayout, "title" | "subtitle" | "description"> {
  const title: Title = {
    text: fitText(m, nodeTitle(p.kind), right - PAD, COMPACT_TITLE.size),
    x: PAD,
    y: COMPACT_TITLE.y,
    size: COMPACT_TITLE.size,
    weight: 600,
  };
  const id = fitText(m, p.id, p.w * COMPACT_ID_SHARE, COMPACT_ID_SIZE);
  const subtitle: Subtitle = { text: id, x: PAD, y: COMPACT_LINE2_Y, size: COMPACT_ID_SIZE };
  if (!p.description) return { title, subtitle };
  const x = PAD + m.text(id, COMPACT_ID_SIZE).x + 2 * GAP;
  const maxWidth = p.w - PAD - x;
  const description: Description = {
    text: fitText(m, p.description, maxWidth, DESC_SIZE),
    x,
    y: COMPACT_LINE2_Y,
    size: DESC_SIZE,
    maxWidth,
    placement: "header",
  };
  return { title, subtitle, description };
}

function footerDescription(p: CanvasNode, m: Measure): Pick<NodeCardLayout, "description" | "footer"> {
  if (!p.description) return {};
  const maxWidth = p.w - 2 * PAD;
  const top = p.h + FOOTER_GAP;
  return {
    footer: { x: GAP, y: top, w: p.w - 2 * GAP, h: FOOTER_H },
    description: { text: fitText(m, p.description, maxWidth, DESC_SIZE), x: PAD, y: top + FOOTER_H / 2, size: DESC_SIZE, maxWidth, placement: "footer" },
  };
}

/** A right-aligned badge on line `y`, fitted to BADGE_SHARE of the card. */
function rightBadge(p: CanvasNode, m: Measure, y: number): Badge | undefined {
  if (!p.status || !p.badge) return undefined;
  return { text: fitText(m, p.badge.text, p.w * BADGE_SHARE, BADGE_SIZE), x: p.w - PAD, y, size: BADGE_SIZE, align: "right" };
}

const badgeWidth = (m: Measure, badge: Badge | undefined): number => (badge ? m.text(badge.text, BADGE_SIZE).x : 0);

const withBadge = (badge: Badge | undefined): { badge?: Badge } => (badge ? { badge } : {});

/** Where each text and the status indicator go for `props` in `style`.
 *  Pure: `measure` sizes the texts, so a test can pass a fixed one. */
export function nodeCardLayout(props: CanvasNode, style: NodeStyleName, measure: Measure): NodeCardLayout {
  const p = props;
  const m = measure;
  switch (style) {
    case "classic": {
      // The card of today: dot at the top right, the badge under it, the
      // description in the hanging footer.
      const badge = rightBadge(p, m, TALL_ID.y);
      const dotRoom = p.status ? 2 * PAD : 0;
      return {
        ...tallHeader(p, m, p.w - 2 * PAD - dotRoom, badgeWidth(m, badge)),
        ...footerDescription(p, m),
        ...(p.status ? { status: { kind: "dot" as const, x: p.w - PAD - 4, y: 9, w: 8, h: 8 } } : {}),
        ...withBadge(badge),
      };
    }
    case "bar": {
      // A status bar down the left edge, inset past the rounded corners;
      // title and id as today, the badge right on line 2.
      const badge = rightBadge(p, m, TALL_ID.y);
      return {
        ...tallHeader(p, m, p.w - 2 * PAD, badgeWidth(m, badge)),
        ...footerDescription(p, m),
        ...(p.status ? { status: { kind: "bar" as const, x: 1, y: 6, w: 4, h: p.h - 12 } } : {}),
        ...withBadge(badge),
      };
    }
    case "banner": {
      // The header band tinted by status; the title and the badge (right)
      // on line 1, the id and the description on line 2.
      const badge = rightBadge(p, m, COMPACT_TITLE.y);
      const right = p.w - PAD - (badge ? badgeWidth(m, badge) + GAP : 0);
      return {
        ...compactHeader(p, m, right),
        ...(p.status ? { status: { kind: "tint" as const, x: 0, y: 0, w: p.w, h: NODE_HEADER } } : {}),
        ...withBadge(badge),
      };
    }
    case "chip": {
      // A rounded chip at the top right holding the status text (the badge,
      // or the bare status name before the first badge arrives); the id
      // and the description on line 2.
      if (!p.status) return compactHeader(p, m, p.w - PAD);
      const text = fitText(m, p.badge?.text ?? p.status, p.w * BADGE_SHARE - 2 * GAP, BADGE_SIZE);
      const w = m.text(text, BADGE_SIZE).x + 2 * GAP;
      const chip = { kind: "chip" as const, x: p.w - PAD - w, y: 6, w, h: 16, text };
      return {
        ...compactHeader(p, m, chip.x - GAP),
        status: chip,
        badge: { text, x: chip.x + w / 2, y: chip.y + chip.h / 2, size: BADGE_SIZE, align: "center" },
      };
    }
  }
}

/** A box in card-local coordinates. */
export interface CardBox {
  readonly x: number;
  readonly y: number;
  readonly w: number;
  readonly h: number;
}

/** The box `props` paints in `style`: the card, the port sockets half outside
 *  its left and right edges, and a footer strip hanging below it. */
export function nodeCardExtent(props: CanvasNode, style: NodeStyleName, measure: Measure): CardBox {
  const footer = nodeCardLayout(props, style, measure).footer;
  const left = props.inputs.length > 0 ? SOCKET_RADIUS : 0;
  const right = props.outputs.length > 0 ? SOCKET_RADIUS : 0;
  const bottom = Math.max(props.h, footer ? footer.y + footer.h : 0);
  return { x: 0 - left, y: 0, w: props.w + left + right, h: bottom };
}

/** The box `props` paints in whichever style paints the most: what a layout
 *  must keep clear so no style switch makes two cards overlap. */
export function nodeFootprint(props: CanvasNode, measure: Measure): CardBox {
  const boxes = nodeStyleNames.map((style) => nodeCardExtent(props, style, measure));
  const x = Math.min(...boxes.map((b) => b.x));
  const y = Math.min(...boxes.map((b) => b.y));
  return {
    x,
    y,
    w: Math.max(...boxes.map((b) => b.x + b.w)) - x,
    h: Math.max(...boxes.map((b) => b.y + b.h)) - y,
  };
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
}

const BANNER_TINT = 0.22;

function drawStatus(
  painter: Painter,
  r: Rect,
  s: NonNullable<NodeCardLayout["status"]>,
  color: Color,
  colors: NodeCardColors,
): void {
  const at = rect(r.x + s.x, r.y + s.y, s.w, s.h);
  switch (s.kind) {
    case "dot":
      painter.dot(v(at.x + s.w / 2, at.y + s.h / 2), s.w / 2, color);
      return;
    case "bar":
      painter.box(at, 2, color);
      return;
    case "chip":
      painter.box(at, s.h / 2, calpha(color, 0.16), color, 1);
      return;
    case "tint": {
      // Inset 1 px so the card border stays visible; the second box squares
      // the band's bottom corners where it meets the port rows.
      const tint = cmix(colors.fill, color, BANNER_TINT);
      painter.box(rect(at.x + 1, at.y + 1, s.w - 2, s.h - 1), 7, tint);
      painter.box(rect(at.x + 1, at.y + s.h / 2, s.w - 2, s.h / 2), 0, tint);
    }
  }
}

/** Draws the card body (box, texts, status, ports, separator) for `node` in
 *  `style`. */
export function renderNodeCard(
  node: { readonly rect: Rect; readonly props: CanvasNode },
  painter: Painter,
  colors: NodeCardColors,
  palette: NodeCardPalette,
  style: NodeStyleName,
): void {
  const r = node.rect;
  const p = node.props;
  const layout = nodeCardLayout(p, style, painter.measure);
  const statusColor = p.status ? palette.status[p.status] : colors.dim;

  painter.box(r, 8, colors.fill, p.contributing ? palette.contributing : colors.edge, p.contributing ? 2 : 1.2);
  if (layout.status) drawStatus(painter, r, layout.status, statusColor, colors);
  if (layout.footer) {
    const f = layout.footer;
    painter.box(rect(r.x + f.x, r.y + f.y, f.w, f.h), 4, calpha(colors.fill, 0.92), calpha(colors.dim, 0.3), 1);
  }

  const t = layout.title;
  painter.label(t.text, v(r.x + t.x, r.y + t.y), colors.text, { align: "left", weight: t.weight, size: t.size });
  for (const s of [layout.subtitle, layout.description])
    if (s) painter.label(s.text, v(r.x + s.x, r.y + s.y), colors.dim, { align: "left", size: s.size });
  if (layout.badge) {
    // Badge text (TKT-10): the state's own hint, or the upstream node
    // responsible for it. On the banner's tint it leans toward the text
    // colour so it still reads against its own hue.
    const b = layout.badge;
    const color = style === "banner" ? cmix(statusColor, colors.text, 0.4) : statusColor;
    painter.label(b.text, v(r.x + b.x, r.y + b.y), color, { align: b.align, size: b.size });
  }

  p.inputs.forEach((port, i) => {
    const y = portY(r.y, i);
    painter.dot(v(r.x, y), SOCKET_RADIUS, colors.socket);
    painter.label(port.name, v(r.x + 11, y), colors.dim, { align: "left", size: PORT_SIZE });
  });
  p.outputs.forEach((port, i) => {
    const y = portY(r.y, i);
    painter.dot(v(r.right, y), SOCKET_RADIUS, colors.socket);
    painter.label(port.name, v(r.right - 11, y), colors.dim, { align: "right", size: PORT_SIZE });
  });
  // A quiet separator between the port rows and the inline param slots.
  if (p.params.length > 0) {
    const y = r.y + portsBottom(p) + 3;
    painter.line(v(r.x + 10, y), v(r.right - 10, y), calpha(colors.dim, 0.35), 1);
  }
}
