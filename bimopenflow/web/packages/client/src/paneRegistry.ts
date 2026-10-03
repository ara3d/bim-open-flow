// The panes an editor offers, as data: each registration says which nodes it
// suits, how to build it, and how to feed it a shown node. The editor's pane
// area reads a registry instead of naming panes, so a pane outside the
// generic set (the 3D pane, @bimopenflow/pane-3d) plugs in without the editor
// depending on it.

import type { NodeDescriptor, PortDescriptor } from "@bimopenflow/contracts";
import {
  createChartPane,
  createInspectorPane,
  createTablePane,
  createVerdictPane,
  type ChartPaneOptions,
  type Pane,
  type PaneContext,
} from "@bimopenflow/panes";
import type { GraphDocument } from "@bimopenflow/state";
import { hostMessage } from "./hostMessage";
import { hasResults, PANE_OFFERS, rankPanes, type PaneOffer } from "./paneChoice";
import type { LiveViewRecipe, ShownNode } from "./shownNode";

/** What the pane area lends a feed for one update. */
export interface PaneFeedIo {
  readonly ctx: PaneContext;
  /** The output the pane reads: the first table output (the table-only studio: the first output). */
  readonly port: PortDescriptor | undefined;
  /** False once a newer update or another pane has replaced this one; check after every await. */
  readonly current: () => boolean;
  /** Catalog model id for a model file path; absent when the editor has no model catalog. */
  readonly resolveModelId?: (path: string) => Promise<string | null>;
  /** Adds a line to the pane's header, for a problem with this node's data rather than the app. */
  readonly note: (text: string) => void;
}

export interface PaneRegistration {
  /** Unique within a registry; the tab's data-kind. */
  readonly kind: string;
  /** The tab's text. */
  readonly label: string;
  readonly offer: PaneOffer;
  readonly create: (chart: ChartPaneOptions) => Pane;
  /** The pane's host element takes the pane body's full height. */
  readonly fillHeight?: boolean;
  /** Pushes the shown node's data into the pane; a throw is reported as an editor error. */
  readonly feed: (pane: Pane, shown: ShownNode, io: PaneFeedIo) => Promise<void>;
  /** Keeps the mounted pane when the shown node changes from `prev` to `next`, and only re-feeds it. */
  readonly keep?: (prev: ShownNode, next: ShownNode) => boolean;
  /** A result drawn from the open document without the host (ShownNode.live). */
  readonly preview?: (
    document: GraphDocument,
    nodeId: string,
    catalog: ReadonlyMap<string, NodeDescriptor>,
  ) => LiveViewRecipe;
}

/** Registrations by kind, in the order given; a repeated kind is an error. */
export function paneRegistry(...panes: readonly PaneRegistration[]): ReadonlyMap<string, PaneRegistration> {
  const registry = new Map<string, PaneRegistration>();
  for (const pane of panes) {
    if (registry.has(pane.kind)) throw new Error(`Pane kind '${pane.kind}' is registered twice.`);
    registry.set(pane.kind, pane);
  }
  return registry;
}

/** The registered kinds that suit a node, best default first. */
export function panesFor(
  desc: NodeDescriptor | undefined,
  registry: ReadonlyMap<string, PaneRegistration>,
): string[] {
  return rankPanes(desc, registry.values());
}

/** The node's first page of rows, once the host has a result for it. */
export async function feedTable(pane: Pane, shown: ShownNode, io: PaneFeedIo): Promise<void> {
  if (!io.port || shown.pending || !hasResults(shown.state)) return;
  let data;
  try {
    data = await io.ctx.requestTable(shown.nodeId, io.port.name);
  } catch (e) {
    // A result read that fails is about this node's data, not the app: the
    // host re-evaluated or the node went away between the evaluation update
    // and this request, and the next update re-feeds the pane.
    if (io.current()) io.note(`No rows to show: ${hostMessage(e)}`);
    return;
  }
  if (io.current()) pane.update({ kind: "table", data });
}

const feedInspector = async (pane: Pane, shown: ShownNode): Promise<void> => {
  if (shown.desc)
    pane.update({ kind: "inspect", node: shown.desc, values: shown.values, state: shown.state, nodeId: shown.nodeId });
};

/** The panes every editor has: table, chart, verdict list, and inspector. */
export const genericPanes: readonly PaneRegistration[] = [
  { kind: "table", label: "Table", offer: PANE_OFFERS.table, create: () => createTablePane(), feed: feedTable },
  { kind: "chart", label: "Chart", offer: PANE_OFFERS.chart, create: (chart) => createChartPane(chart), feed: feedTable },
  { kind: "verdict", label: "Verdicts", offer: PANE_OFFERS.verdict, create: () => createVerdictPane(), feed: feedTable },
  { kind: "inspector", label: "Inspector", offer: PANE_OFFERS.inspector, create: () => createInspectorPane(), feed: feedInspector },
];
