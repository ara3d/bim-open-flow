// Which panes make sense for a node — pure heuristics over the catalog
// descriptor, ordered most-specific first (the first entry becomes the
// default tab).

import type { NodeDescriptor, PortDescriptor } from "@bimopenflow/contracts";

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

function isVerdictKind(kind: string): boolean {
  return kind.includes("verdict") || kind.startsWith("compliance.");
}

function isView3DKind(desc: NodeDescriptor): boolean {
  return (
    desc.kind.startsWith("view3d") ||
    desc.outputs.some((p) => p.name === "instances" && p.type === "Table")
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
  if (table) panes.push("table", "chart");
  panes.push("params", "inspector");
  return panes;
}
