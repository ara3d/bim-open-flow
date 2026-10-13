// Card sizes (TKT-124, TKT-125): the one place that decides how big a card is
// drawn, shared by the view model (buildCanvasModel), the resize gesture
// (canvasParts.ts), the layout algorithms, and the app's placement of a new
// node. It imports nothing that imports viewModel.ts, so the render modules
// can take their card metrics from here without a cycle.
//
// A card without a saved width is as wide as its content: the widest of its
// title, its id, a row of port labels, and a param row's label and value,
// between its minimum width and MAX_CONTENT_WIDTH. A saved width (a resize)
// is kept, between the minimum and MAX_NODE_SIZE; the renderers fit or wrap
// every text to the width the card has, so a narrower card cuts text with an
// ellipsis rather than letting it overflow. Height follows the content; only
// a note's height is resizable, and never below what its wrapped text needs.
// A card's description (the part its DescriptionLevel shows, TKT-278) wraps
// at the card's width below the param rows and adds its lines to the height,
// so the layouts, which keep footprints apart, see the card as drawn.
//
// Widths are measured with SIZE_MEASURE, the fixed-width estimate of
// gratify's NullPainter, never with the browser's fonts, so the same graph
// gets the same card sizes in the browser, the headless tests, and the
// committed sample layouts. Real text a little wider than the estimate is
// fitted when it is drawn.

import { NullPainter, type Measure } from "gratify";
import { wrapParagraphs } from "./canvasLongSlot.js";
import { placeSlots, SLOT_X_PAD, slotWidth, type CanvasParam } from "./canvasSlots.js";
import { nodeTitle } from "./graphPreview.js";

export interface Size {
  readonly w: number;
  readonly h: number;
}

/** Card frame: the header is NODE_HEADER tall and port rows are
 *  PORT_SPACING apart below it (see nodeRender.ts and portGeometry.ts). */
export const NODE_HEADER = 46;
export const PORT_SPACING = 24;
/** Narrowest card: the least width without param rows, and with them. */
export const NODE_WIDTH = 184;
export const WIDE_NODE_WIDTH = 260;
/** Widest a card grows to fit its content. Past this its texts are fitted:
 *  a SQL query wraps, a long title gets an ellipsis. */
export const MAX_CONTENT_WIDTH = 520;
/** Widest (and, for a note, tallest) a card may be dragged. A width past
 *  this stops reading as one node on a canvas at ordinary zoom. */
export const MAX_NODE_SIZE = 900;

/** Header metrics nodeRender.ts draws with: the side padding, the gap between
 *  two texts on a line, and the fonts of the title, the id, and the port
 *  labels. A port label sits PORT_LABEL_INSET in from its socket. */
export const CARD_PAD = 12;
export const CARD_GAP = 6;
export const TITLE_SIZE = 17;
export const ID_SIZE = 13;
export const PORT_LABEL_SIZE = 13;
export const PORT_LABEL_INSET = 11;
/** Room a card's width keeps right of the title. The status chip is fitted
 *  into the header, not sized for; this spare room is what the committed
 *  sample layouts were sized with (the classic style's status dot, TKT-98). */
export const STATUS_DOT_ROOM = 2 * CARD_PAD;
/** How much wider the 600-weight title is than the regular-weight text a
 *  Measure reports (gratify's CanvasPainter measures at weight 400). */
export const BOLD_WIDEN = 1.1;

/** A comment pinned to the canvas (see NoteNode in BimOpenFlow.Nodes.Viz): no
 *  ports, one text param drawn as the whole card rather than a labelled row. */
export const NOTE_KIND = "view.note";
export const NOTE_WIDTH = 300;
export const NOTE_LINE_H = 18;
export const NOTE_MAX_LINES = 12;
export const NOTE_PAD = 16;
export const NOTE_TEXT_SIZE = 14;

/** The description block at the bottom of a card (TKT-278): DESC_SIZE text
 *  on DESC_LINE_H lines, DESC_GAP below the param rows and DESC_PAD above the
 *  card's bottom edge, wrapped to the card's width less CARD_PAD each side.
 *  DESC_MAX_LINES only stops a pathological text; the longest catalog
 *  description (1,381 characters) takes about 40 lines on the narrowest card. */
export const DESC_SIZE = 11;
export const DESC_LINE_H = 14;
export const DESC_GAP = 2;
export const DESC_PAD = 8;
export const DESC_MAX_LINES = 80;

/** The estimate every size is measured with (see the file comment). */
export const SIZE_MEASURE: Measure = new NullPainter().measure;

/** The fields of a card its size is computed from. */
export interface SizedNode {
  readonly id: string;
  readonly kind: string;
  readonly inputs: readonly { readonly name: string }[];
  readonly outputs: readonly { readonly name: string }[];
  readonly params: readonly CanvasParam[];
  readonly noteText?: string;
  /** The description text the card draws at the current level; absent for none. */
  readonly description?: string;
}

/** Width of the 600-weight `text` at `size`. */
export const boldWidth = (m: Measure, text: string, size: number): number => m.text(text, size).x * BOLD_WIDEN;

/** Narrowest a card may be: wide enough for the controls of its param rows. */
export function minNodeWidth(node: Pick<SizedNode, "kind" | "params">): number {
  if (node.kind === NOTE_KIND) return NOTE_WIDTH;
  return node.params.length > 0 ? WIDE_NODE_WIDTH : NODE_WIDTH;
}

/** Width one port row needs: the input label from the left socket, the
 *  output label from the right one, with a gap between them. */
function portRowWidth(m: Measure, input: string | undefined, output: string | undefined): number {
  const side = (label: string | undefined) =>
    label === undefined ? CARD_PAD : PORT_LABEL_INSET + m.text(label, PORT_LABEL_SIZE).x;
  return side(input) + side(output) + (input !== undefined && output !== undefined ? 2 * CARD_GAP : 0);
}

/** Width `node`'s texts need to be drawn whole, before any clamp. The badge
 *  and the description do not count: the badge changes with every evaluation
 *  and is fitted to the card; the description wraps to the card's width. */
export function contentWidth(node: SizedNode, m: Measure = SIZE_MEASURE): number {
  const header = Math.max(
    boldWidth(m, nodeTitle(node.kind), TITLE_SIZE) + 2 * CARD_PAD + STATUS_DOT_ROOM,
    m.text(node.id, ID_SIZE).x + 2 * CARD_PAD,
  );
  const rows = Math.max(node.inputs.length, node.outputs.length);
  const ports = Array.from({ length: rows }, (_, i) => portRowWidth(m, node.inputs[i]?.name, node.outputs[i]?.name));
  const slots = node.params.map((p) => slotWidth(p, m) + 2 * SLOT_X_PAD);
  return Math.ceil(Math.max(header, ...ports, ...slots));
}

/** Bottom of the port rows, relative to the card top. */
export const portRowsBottom = (inputCount: number, outputCount: number): number =>
  NODE_HEADER + Math.max(inputCount, outputCount, 1) * PORT_SPACING;

/** Node height grows with its densest port side, then with its param slots —
 *  each slot contributes the height its control kind needs. */
export function nodeHeight(
  inputCount: number,
  outputCount: number,
  params: readonly CanvasParam[] = [],
): number {
  return placeSlots(params, portRowsBottom(inputCount, outputCount)).bottom;
}

/** Lines `description` wraps to on a card `w` wide, as nodeRender draws it:
 *  at most `maxLines`, the last ending in an ellipsis when more were needed. */
export const descriptionLines = (description: string, w: number, m: Measure = SIZE_MEASURE, maxLines = DESC_MAX_LINES): string[] =>
  wrapParagraphs(m, description, w - 2 * CARD_PAD, DESC_SIZE, Math.min(maxLines, DESC_MAX_LINES));

/** Height the description block adds to a card `w` wide: none without text. */
export function descriptionHeight(description: string | undefined, w: number, m: Measure = SIZE_MEASURE): number {
  if (!description) return 0;
  return DESC_GAP + descriptionLines(description, w, m).length * DESC_LINE_H + DESC_PAD;
}

/** Height of a note `w` wide: its text wrapped exactly as canvasParts paints
 *  it, at least one line and at most NOTE_MAX_LINES. */
export function noteHeight(text: string, w: number = NOTE_WIDTH, m: Measure = SIZE_MEASURE): number {
  const lines = wrapParagraphs(m, text, w - 2 * NOTE_PAD, NOTE_TEXT_SIZE, NOTE_MAX_LINES).length;
  return NOTE_PAD * 2 + Math.max(lines, 1) * NOTE_LINE_H;
}

const clamp = (value: number, min: number, max: number): number => Math.min(Math.max(value, min), Math.max(min, max));

/**
 * The size `node` is drawn at. `want` is a saved layout entry or a resize
 * drag: a width in it is kept, between minNodeWidth and MAX_NODE_SIZE;
 * without one the card is as wide as its content, up to MAX_CONTENT_WIDTH.
 * Only a note's height is resizable, from the height its wrapped text needs
 * at that width up to MAX_NODE_SIZE; any other card's height follows its
 * ports, param rows, and description wrapped at its width, since extra
 * height would only be empty space.
 */
export function nodeSize(
  node: SizedNode,
  want: { readonly w?: number; readonly h?: number } = {},
  m: Measure = SIZE_MEASURE,
): Size {
  const min = minNodeWidth(node);
  if (node.kind === NOTE_KIND) {
    const w = clamp(want.w ?? NOTE_WIDTH, min, MAX_NODE_SIZE);
    const h = noteHeight(node.noteText ?? "", w, m);
    return { w, h: clamp(want.h ?? h, h, MAX_NODE_SIZE) };
  }
  const w = want.w !== undefined
    ? clamp(want.w, min, MAX_NODE_SIZE)
    : clamp(contentWidth(node, m), min, MAX_CONTENT_WIDTH);
  return { w, h: nodeHeight(node.inputs.length, node.outputs.length, node.params) + descriptionHeight(node.description, w, m) };
}
