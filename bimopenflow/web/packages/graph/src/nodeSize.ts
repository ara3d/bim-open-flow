// Card sizes: the one place that decides how big a card is drawn, shared by
// the view model (buildCanvasModel), the resize gesture (canvasParts.ts),
// the layout algorithms, and the app's placement of a new node. Pure and
// gratify-free apart from types, so it is testable headless and imports
// nothing that imports viewModel.ts.

import type { CanvasParam } from "./canvasSlots.js";
import { placeSlots } from "./canvasSlots.js";

export interface Size {
  readonly w: number;
  readonly h: number;
}

/** Card frame: the header is NODE_HEADER tall and port rows are
 *  PORT_SPACING apart below it (see nodeRender.ts and portGeometry.ts). */
export const NODE_HEADER = 46;
export const PORT_SPACING = 24;
export const NODE_WIDTH = 184;
/** Nodes with inline param slots get extra width so field values stay legible. */
export const WIDE_NODE_WIDTH = 260;
/** Widest (and, for a note, tallest) a card may be dragged. A width past
 *  this stops reading as one node on a canvas at ordinary zoom. */
export const MAX_NODE_SIZE = 900;

/** A comment pinned to the canvas (see NoteNode in BimOpenFlow.Nodes.Viz): no
 *  ports, one text param drawn as the whole card rather than a labelled row. */
export const NOTE_KIND = "view.note";
export const NOTE_WIDTH = 300;
export const NOTE_LINE_H = 18;
export const NOTE_MAX_LINES = 12;
export const NOTE_PAD = 16;
/** Rough characters per wrapped line at the note's width and font size; only
 *  used to size the card. canvasParts wraps the actual text precisely for
 *  painting, so a note is always at least as tall as it needs and at most
 *  NOTE_MAX_LINES tall (canvasParts then ellipsizes past that). */
const NOTE_CHARS_PER_LINE = 34;

/** The fields of a card its size is computed from. */
export interface SizedNode {
  readonly kind: string;
  readonly inputs: readonly unknown[];
  readonly outputs: readonly unknown[];
  readonly params: readonly CanvasParam[];
  readonly noteText?: string;
}

export function nodeWidth(params: readonly CanvasParam[]): number {
  return params.length > 0 ? WIDE_NODE_WIDTH : NODE_WIDTH;
}

/** Fixed width, height from an approximate wrap of `text` up to
 *  NOTE_MAX_LINES lines (one more line for an explicit line break). */
export function noteHeight(text: string): number {
  const paragraphs = text.split("\n");
  const lines = paragraphs.reduce(
    (sum, p) => sum + Math.max(1, Math.ceil(p.length / NOTE_CHARS_PER_LINE)),
    0,
  );
  return NOTE_PAD * 2 + Math.min(Math.max(lines, 1), NOTE_MAX_LINES) * NOTE_LINE_H;
}

/** Node height grows with its densest port side, then with its param slots —
 *  each slot contributes the height its control kind needs. */
export function nodeHeight(
  inputCount: number,
  outputCount: number,
  params: readonly CanvasParam[] = [],
): number {
  const portsBottom = NODE_HEADER + Math.max(inputCount, outputCount, 1) * PORT_SPACING;
  return placeSlots(params, portsBottom).bottom;
}

/** The size `node`'s content needs: its default size, and the smallest a
 *  resize may make it (TKT-124). */
export function contentSize(node: SizedNode): Size {
  return node.kind === NOTE_KIND
    ? { w: NOTE_WIDTH, h: noteHeight(node.noteText ?? "") }
    : { w: nodeWidth(node.params), h: nodeHeight(node.inputs.length, node.outputs.length, node.params) };
}

/**
 * The size a card is drawn at, given the size its content needs (`content`)
 * and the size asked for (a drag, or a saved layout entry). Width runs from
 * the content width to MAX_NODE_SIZE. Height does the same for a note; any
 * other card's height follows its ports and param slots, since extra height
 * would only be empty space. Holding a saved size to the content size also
 * keeps a card whole when a later edit (a longer note, a new param) makes its
 * content grow.
 */
export function clampNodeSize(
  content: Size,
  want: { readonly w?: number; readonly h?: number },
  heightResizable: boolean,
): Size {
  const clamp = (value: number | undefined, min: number) =>
    Math.min(Math.max(value ?? min, min), Math.max(min, MAX_NODE_SIZE));
  return {
    w: clamp(want.w, content.w),
    h: heightResizable ? clamp(want.h, content.h) : content.h,
  };
}

/** The size `node` is drawn at when `want` is asked for (a saved layout
 *  entry or a resize drag). Only a note's height is resizable. */
export function fitNodeSize(node: SizedNode, want: { readonly w?: number; readonly h?: number }): Size {
  return clampNodeSize(contentSize(node), want, node.kind === NOTE_KIND);
}
