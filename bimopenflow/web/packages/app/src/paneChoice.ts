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
  | "params"
  | "inspector";

export function firstTableOutput(
  desc: NodeDescriptor | undefined,
): PortDescriptor | undefined {
  return desc?.outputs.find((p) => p.type === "Table");
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
 * Panes offered for a node, best default first. Params and inspector are
 * always available; table/chart need a Table output; verdict and 3D come
 * from kind conventions.
 */
export function choosePanes(desc: NodeDescriptor | undefined): PaneKind[] {
  if (!desc) return ["params", "inspector"];
  const panes: PaneKind[] = [];
  const table = firstTableOutput(desc);
  if (table && isVerdictKind(desc.kind)) panes.push("verdict");
  if (table && isView3DKind(desc)) panes.push("view3d");
  if (table && desc.kind.startsWith("chart.")) panes.push("chart", "table");
  else if (table) panes.push("table", "chart");
  panes.push("params", "inspector");
  return panes;
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
