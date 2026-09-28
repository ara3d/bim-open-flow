// What the canvas palette (TKT-96) lists: catalog kinds matching the search
// text and, for a dropped wire, only kinds with a port that can take it.
// Pure, DOM-free.

import type { NodeDescriptor, PortType } from "@bimopenflow/contracts";
import { filterCatalog } from "./catalogFilter.js";
import { canConnect, type AnchorDir } from "@bimopenflow/graph";

export interface PaletteEntry {
  readonly desc: NodeDescriptor;
  /** The new node's input (for a wire from an output) or output (for a wire
   *  from an input) that takes the dropped wire. */
  readonly port?: string;
}

/** The dropped wire's end: the anchor's direction and its port type. */
export interface PaletteWire {
  readonly dir: AnchorDir;
  readonly type: PortType;
}

// canConnect refuses a wire from a node to itself; the node a pick would add
// has no id yet, so these two stand in for "some other node".
const WIRE_NODE = "wire";
const NEW_NODE = "new";

/** The first port of `desc` that `wire` may connect to, by canConnect's rule. */
function takingPort(desc: NodeDescriptor, wire: PaletteWire): string | undefined {
  const from = { dir: wire.dir, nodeId: WIRE_NODE, type: wire.type };
  const sides: [AnchorDir, NodeDescriptor["inputs"]][] = [["in", desc.inputs], ["out", desc.outputs]];
  for (const [dir, ports] of sides)
    for (const p of ports)
      if (canConnect(from, { dir, nodeId: NEW_NODE, type: p.type })) return p.name;
  return undefined;
}

/**
 * Kinds matching `query` (case-insensitive; every whitespace-separated term
 * must appear in the kind or the description, as the sidebar filters), kinds
 * whose own name matches first, each group sorted by kind. With `wire`, only
 * kinds with a port of the opposite direction whose type is compatible
 * (canConnect's rule), and `port` names the first such port.
 */
export function filterPalette(
  catalog: readonly NodeDescriptor[],
  query: string,
  wire?: PaletteWire,
): PaletteEntry[] {
  const terms = query.toLowerCase().split(/\s+/).filter((t) => t.length > 0);
  const byKind = (a: NodeDescriptor, b: NodeDescriptor) => a.kind.localeCompare(b.kind);
  const matches = filterCatalog(catalog, query).sort(byKind);
  const kindMatch = (d: NodeDescriptor) => terms.every((t) => d.kind.toLowerCase().includes(t));
  const ranked = [...matches.filter(kindMatch), ...matches.filter((d) => !kindMatch(d))];
  if (!wire) return ranked.map((desc) => ({ desc }));
  return ranked.flatMap((desc) => {
    const port = takingPort(desc, wire);
    return port === undefined ? [] : [{ desc, port }];
  });
}
