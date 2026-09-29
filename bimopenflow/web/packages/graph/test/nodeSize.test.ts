// Card sizes from content (TKT-125): a card without a saved width is as wide
// as its widest text, between its least width and MAX_CONTENT_WIDTH; a saved
// width is kept; a note is as tall as its text wrapped at its actual width.

import { describe, expect, it } from "vitest";
import type { CanvasParam } from "../src/canvasSlots.js";
import {
  contentWidth,
  MAX_CONTENT_WIDTH,
  NODE_WIDTH,
  nodeSize,
  NOTE_KIND,
  NOTE_LINE_H,
  NOTE_MAX_LINES,
  NOTE_PAD,
  NOTE_WIDTH,
  noteHeight,
  SIZE_MEASURE,
  WIDE_NODE_WIDTH,
} from "../src/nodeSize.js";

const port = (name: string) => ({ name });
const card = (extra: Partial<Parameters<typeof nodeSize>[0]> = {}) =>
  ({ id: "a", kind: "k.a", inputs: [port("in")], outputs: [port("out")], params: [], ...extra });
const text = (name: string, value: string): CanvasParam => ({ name, kind: "Text", value });
const width = (s: string, size: number) => SIZE_MEASURE.text(s, size).x;

describe("nodeSize width from content", () => {
  it("keeps a card with short texts at its least width", () => {
    expect(nodeSize(card()).w).toBe(NODE_WIDTH);
    expect(nodeSize(card({ params: [text("where", "")] })).w).toBe(WIDE_NODE_WIDTH);
  });

  it("grows with a long id, a long port label pair, and a long param value", () => {
    const longId = "a_node_id_long_enough_to_widen_the_card";
    expect(nodeSize(card({ id: longId })).w).toBeGreaterThanOrEqual(width(longId, 13) + 24);
    const ports = card({ inputs: [port("a_long_input_port_name")], outputs: [port("a_long_output_port_name")] });
    expect(nodeSize(ports).w).toBeGreaterThan(NODE_WIDTH);
    const path = text("file", "C:/data/models/a-long-model-file-name.parquet");
    expect(nodeSize(card({ params: [path] })).w).toBeGreaterThan(WIDE_NODE_WIDTH);
  });

  it("stops at MAX_CONTENT_WIDTH", () => {
    const sql = { name: "sql", kind: "Expression" as const, value: "SELECT ".repeat(200) };
    expect(contentWidth(card({ params: [sql] }))).toBeGreaterThan(MAX_CONTENT_WIDTH);
    expect(nodeSize(card({ params: [sql] })).w).toBe(MAX_CONTENT_WIDTH);
  });

  it("sizes a dropdown by its widest option, so picking one does not resize the card", () => {
    const options = ["a", "an option wide enough to count"];
    const pick = (value: string): CanvasParam => ({ name: "mode", kind: "Enum", value, enumValues: options });
    expect(nodeSize(card({ params: [pick("a")] })).w).toBe(nodeSize(card({ params: [pick(options[1]!)] })).w);
    expect(nodeSize(card({ params: [pick("a")] })).w).toBeGreaterThan(WIDE_NODE_WIDTH);
  });

  it("keeps a saved width, even below the content width", () => {
    const wide = card({ params: [text("file", "x".repeat(80))] });
    expect(nodeSize(wide, { w: 300 }).w).toBe(300);
    expect(nodeSize(wide, { w: 700 }).w).toBe(700);
  });
});

describe("note height", () => {
  const words = "storey mapping ".repeat(20);
  const note = (w?: number) =>
    nodeSize({ id: "n", kind: NOTE_KIND, inputs: [], outputs: [], params: [], noteText: words }, w === undefined ? {} : { w });

  it("follows the text wrapped at the note's width", () => {
    expect(note().w).toBe(NOTE_WIDTH);
    expect(note(600).h).toBeLessThan(note().h);
    expect(note(600).h).toBe(noteHeight(words, 600));
  });

  it("holds one to NOTE_MAX_LINES lines", () => {
    expect(noteHeight("")).toBe(2 * NOTE_PAD + NOTE_LINE_H);
    expect(noteHeight("word ".repeat(1000))).toBe(2 * NOTE_PAD + NOTE_MAX_LINES * NOTE_LINE_H);
  });

  it("gives an explicit line break its own line", () => {
    expect(noteHeight("a\nb")).toBe(2 * NOTE_PAD + 2 * NOTE_LINE_H);
  });
});
