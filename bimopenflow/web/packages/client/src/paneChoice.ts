// Which panes make sense for a node — pure heuristics over the catalog
// descriptor, ordered most-specific first (the first entry becomes the
// default tab).

import type { NodeDescriptor, NodeState, PortDescriptor } from "@bimopenflow/contracts";
import type { ChartPaneOptions } from "@bimopenflow/panes";

export type PaneKind =
  | "verdict"
  | "view3d"
  | "table"
  | "chart"
  | "inspector";

/**
 * The first output the host can page as rows: a Table, or a Relation, which
 * the host runs on request (one limited query plus a count).
 */
export function firstTableOutput(
  desc: NodeDescriptor | undefined,
): PortDescriptor | undefined {
  return desc?.outputs.find((p) => p.type === "Table" || p.type === "Relation");
}

/**
 * Result tables exist on the host only for nodes that evaluated to Ok; asking
 * for anything else (a just-added node, an unready or failed one) is a
 * guaranteed 404, so data panes must not fetch.
 */
export function hasResults(state: NodeState | undefined): boolean {
  return state?.status === "Ok";
}

function isVerdictKind(kind: string): boolean {
  return kind.includes("verdict") || kind.startsWith("compliance.");
}

function isView3DKind(desc: NodeDescriptor): boolean {
  return (
    desc.kind.startsWith("view3d") ||
    desc.outputs.some(
      (p) => (p.name === "instances" || p.name === "boxes") && p.type === "Table",
    )
  );
}

/**
 * Where a pane's tab goes for a node, lower first, or undefined when the pane
 * does not apply to it. One per conventional pane kind; a pane registration
 * (paneRegistry.ts) reuses its kind's entry as its offer.
 */
export type PaneOffer = (desc: NodeDescriptor | undefined) => number | undefined;

/**
 * The conventions: inspector always; table and chart need a table output, the
 * chart first for a chart.* node; verdict and 3D come from kind conventions.
 */
export const PANE_OFFERS: Readonly<Record<PaneKind, PaneOffer>> = {
  verdict: (desc) => desc && firstTableOutput(desc) && isVerdictKind(desc.kind) ? 1 : undefined,
  view3d: (desc) => desc && firstTableOutput(desc) && isView3DKind(desc) ? 2 : undefined,
  chart: (desc) => desc && firstTableOutput(desc) ? (desc.kind.startsWith("chart.") ? 3 : 4) : undefined,
  table: (desc) => desc && firstTableOutput(desc) ? (desc.kind.startsWith("chart.") ? 4 : 3) : undefined,
  inspector: () => 9,
};

/** The kinds whose offer applies to a node, ordered by their offers (stable). */
export function rankPanes<K extends string>(
  desc: NodeDescriptor | undefined,
  offers: Iterable<{ readonly kind: K; readonly offer: PaneOffer }>,
): K[] {
  return [...offers]
    .map(({ kind, offer }) => ({ kind, rank: offer(desc) }))
    .filter((o): o is { kind: K; rank: number } => o.rank !== undefined)
    .sort((x, y) => x.rank - y.rank)
    .map((o) => o.kind);
}

/**
 * Panes offered for a node by the conventions, best default first: which
 * pane would show it, whether or not an editor has registered that pane.
 */
export function choosePanes(desc: NodeDescriptor | undefined): PaneKind[] {
  return rankPanes(desc, (Object.keys(PANE_OFFERS) as PaneKind[]).map((kind) => ({ kind, offer: PANE_OFFERS[kind] })));
}

/**
 * Panes the DuckDB studio offers (its table-only pane area): every node shows
 * its table, and a chart.* node also gets a Chart tab, shown first (TKT-20).
 */
export function studioPanes(desc: NodeDescriptor | undefined): PaneKind[] {
  return desc?.kind.startsWith("chart.") && firstTableOutput(desc)
    ? ["chart", "table"]
    : ["table"];
}

/** Comma list -> trimmed non-empty names; undefined when nothing remains. */
function splitColumns(list: string | undefined): string[] | undefined {
  const parts = (list ?? "")
    .split(",")
    .map((s) => s.trim())
    .filter((s) => s.length > 0);
  return parts.length > 0 ? parts : undefined;
}

/**
 * Chart pane options from a node's kind + param values. chart.* nodes map
 * their params onto the viz options; anything else gets the bar default.
 */
export function chartPaneOptions(
  kind: string | undefined,
  values: Record<string, string>,
): ChartPaneOptions {
  if (kind === "chart.line")
    return {
      chart: "line",
      xColumn: values.xColumn || undefined,
      seriesColumns: splitColumns(values.yColumns),
      title: values.title || undefined,
    };
  if (kind === "chart.bar")
    return {
      chart: "bar",
      categoryColumn: values.labelColumn || undefined,
      seriesColumns: splitColumns(values.valueColumns),
      title: values.title || undefined,
    };
  return { chart: "bar" };
}
