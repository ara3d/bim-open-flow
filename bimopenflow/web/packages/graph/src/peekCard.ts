// The peek card: a gratify part drawn as an adornment beside a node's output
// socket, plus the pure text it and a wire's row-count label show. Design.md
// "peekCard.ts: the drawing". `peekGrid` and `rowCountText` take no gratify
// types, so they stay testable headless; `peekAdorn` is the only piece that
// touches gratify.

import { addAdorn, at, part, v, type Color, type GNode, type PartExt } from "gratify";
import type { PortSchema, SchemaColumn } from "@bimopenflow/contracts";
import { PEEK_ROWS, type PortPeekView } from "./portResults.js";
import { portY } from "./portGeometry.js";

/** Column names a card shows before folding the rest into the note. */
export const PEEK_COLUMNS = 6;

export interface PeekGrid {
  readonly title: string; // "tall.relation · 2 rows · 2 columns"
  readonly columns: readonly string[]; // at most PEEK_COLUMNS
  readonly rows: readonly (readonly string[])[]; // at most PEEK_ROWS; null cells as "null"
  readonly note: string | null; // "+k more columns", the absent reason, or "loading…"
}

/** `0 -> "0 rows"`, `1 -> "1 row"`, `456598 -> "456,598 rows"`. */
export function rowCountText(rows: number): string {
  const noun = rows === 1 ? "row" : "rows";
  return `${rows.toLocaleString("en-US")} ${noun}`;
}

/** "Text", or "Text?" for a column that can be missing. */
const typeText = (c: SchemaColumn): string => (c.nullable ? `${c.type}?` : c.type);

/** The card for an endpoint with no result yet: the columns the static schema
 *  pass inferred (names, then types, then kinds when any column has one), or
 *  what is known instead when the status is not Ok. */
function inferredGrid(endpoint: string, schema: PortSchema, reason: string): PeekGrid {
  switch (schema.status) {
    case "Ok": {
      const all = schema.columns ?? [];
      const shown = all.slice(0, PEEK_COLUMNS);
      const rows = [shown.map(typeText)];
      if (shown.some((c) => c.kind)) rows.push(shown.map((c) => c.kind ?? ""));
      const hidden = all.length - shown.length;
      return {
        title: `${endpoint} · inferred · ${all.length} columns`,
        columns: shown.map((c) => c.name),
        rows: shown.length > 0 ? rows : [],
        note: hidden > 0 ? `+${hidden} more columns · ${reason}` : reason,
      };
    }
    case "Dynamic":
      return { title: endpoint, columns: [], rows: [], note: `Columns known after Run: ${schema.reason ?? reason}` };
    case "Unknown":
      return { title: endpoint, columns: [], rows: [], note: `Columns unknown: ${schema.reason ?? reason}` };
    case "Error":
      return { title: endpoint, columns: [], rows: [], note: `Columns unavailable: ${schema.errors[0] ?? schema.reason ?? reason}` };
  }
}

/** A note when the recorded columns are not the ones the pass inferred. */
function differenceNote(recorded: readonly string[], schema: PortSchema | undefined): string | null {
  if (schema?.status !== "Ok" || !schema.columns) return null;
  const inferred = schema.columns.map((c) => c.name);
  if (inferred.length === recorded.length && inferred.every((n, i) => n === recorded[i])) return null;
  const missing = inferred.filter((n) => !recorded.includes(n));
  const extra = recorded.filter((n) => !inferred.includes(n));
  const parts = [
    ...(missing.length > 0 ? [`not produced: ${missing.join(", ")}`] : []),
    ...(extra.length > 0 ? [`not inferred: ${extra.join(", ")}`] : []),
  ];
  return `differs from inferred columns${parts.length > 0 ? ` (${parts.join("; ")})` : " (order)"}`;
}

/** The text a peek card shows for one endpoint's peek state. */
export function peekGrid(view: PortPeekView): PeekGrid {
  const { peek } = view;
  if (peek.kind === "loading") return { title: view.endpoint, columns: [], rows: [], note: "loading…" };
  if (peek.kind === "absent") return { title: view.endpoint, columns: [], rows: [], note: peek.reason };
  if (peek.kind === "inferred") return inferredGrid(view.endpoint, peek.schema, peek.reason);

  const { slice } = peek;
  const columnCount = slice.columns.length;
  const columns = slice.columns.slice(0, PEEK_COLUMNS).map((c) => c.name);
  const rows = slice.rows.slice(0, PEEK_ROWS).map((row) =>
    row.slice(0, PEEK_COLUMNS).map((cell) => (cell === null || cell === undefined ? "null" : String(cell))),
  );
  const hiddenColumns = columnCount - columns.length;
  const differs = differenceNote(slice.columns.map((c) => c.name), peek.inferred);
  const notes = [hiddenColumns > 0 ? `+${hiddenColumns} more columns` : null, differs].filter((n) => n !== null);
  return {
    title: `${view.endpoint} · ${rowCountText(slice.totalRows)} · ${columnCount} columns`,
    columns,
    rows,
    note: notes.length > 0 ? notes.join(" · ") : null,
  };
}

// ── The card part ────────────────────────────────────────────────────────────

const CARD_PAD = 10;
const CARD_WIDTH = 260;
const LINE_HEIGHT = 16;
const TITLE_HEIGHT = 20;

interface PeekCardProps {
  readonly grid: PeekGrid;
}

interface PeekCardStyle {
  readonly fill: Color;
  readonly edge: Color;
  readonly text: Color;
  readonly dim: Color;
}

/** Characters of a note that fit one line of the card at the note's size. */
export const NOTE_LINE_CHARS = 44;

/** A note broken at spaces into lines of at most NOTE_LINE_CHARS (a longer
 *  word keeps its own line), so a long reason is shown whole. */
export function noteLines(note: string | null): string[] {
  if (!note) return [];
  const lines: string[] = [];
  let line = "";
  for (const word of note.split(" ")) {
    if (line !== "" && line.length + 1 + word.length > NOTE_LINE_CHARS) {
      lines.push(line);
      line = word;
    } else line = line === "" ? word : `${line} ${word}`;
  }
  if (line !== "") lines.push(line);
  return lines;
}

/** Rows the card body draws below its title: a header row of column names
 *  (when there are any), one line per data row, then the note's lines. */
function cardLineCount(grid: PeekGrid): number {
  return (grid.columns.length > 0 ? 1 : 0) + grid.rows.length + noteLines(grid.note).length;
}

const PeekCard = part<PeekCardProps, PeekCardStyle>("bof-peek-card", {
  // Never a hit target: pan, click-to-pin, and wire drags all read through to
  // whatever is under the card (design.md, "hit: () => false").
  hit: () => false,
  size: (props) => v(CARD_WIDTH, CARD_PAD * 2 + TITLE_HEIGHT + cardLineCount(props.grid) * LINE_HEIGHT),
  style: (t) => ({
    fill: t.mix(t.surface, t.surfaceHi, 0.7),
    edge: t.accent,
    text: t.text,
    dim: t.textDim,
  }),
  render(node, painter, style) {
    const { grid } = node.props;
    const r = node.rect;
    painter.box(r, 8, style.fill, style.edge, 1.2);

    let y = r.y + CARD_PAD + 10;
    painter.label(grid.title, v(r.x + CARD_PAD, y), style.text, { align: "left", weight: 600, size: 12 });
    y += TITLE_HEIGHT;

    if (grid.columns.length > 0) {
      const colWidth = (r.w - 2 * CARD_PAD) / grid.columns.length;
      grid.columns.forEach((col, i) => {
        painter.label(col, v(r.x + CARD_PAD + i * colWidth, y), style.dim, { align: "left", size: 10, weight: 600 });
      });
      y += LINE_HEIGHT;

      for (const row of grid.rows) {
        row.forEach((cell, i) => {
          painter.label(cell, v(r.x + CARD_PAD + i * colWidth, y), style.text, { align: "left", size: 10 });
        });
        y += LINE_HEIGHT;
      }
    }

    for (const line of noteLines(grid.note)) {
      painter.label(line, v(r.x + CARD_PAD, y), style.dim, { align: "left", size: 10 });
      y += LINE_HEIGHT;
    }
  },
});

/** Adornment for GraphNodePart: draws the card beside the peeked output
 *  socket of the node whose id matches the endpoint; no elements otherwise
 *  (no view, a different node, or a port that node has no output for). */
export function peekAdorn(view: PortPeekView | undefined): PartExt<any> {
  return addAdorn((node: GNode<any>) => {
    if (!view) return [];
    const dot = view.endpoint.indexOf(".");
    if (dot < 0) return [];
    const nodeId = view.endpoint.slice(0, dot);
    if (node.props.id !== nodeId) return [];
    const port = view.endpoint.slice(dot + 1);
    const outputs = node.props.outputs as readonly { name: string }[] | undefined;
    const index = outputs?.findIndex((p) => p.name === port) ?? -1;
    if (index < 0) return [];

    const r = node.rect;
    const grid = peekGrid(view);
    const pos = v(r.right + 12, portY(r.y, index) - CARD_PAD - 10);
    return [at(PeekCard(`peek-${view.endpoint}`, { grid }), pos)];
  });
}
