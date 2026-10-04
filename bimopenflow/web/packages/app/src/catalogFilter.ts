import type { NodeDescriptor } from "@bimopenflow/contracts";
import { nodePack } from "@bimopenflow/graph";
import { readPref, writePref } from "./prefs.js";

/**
 * Case-insensitive catalog filter: every whitespace-separated term must appear
 * in the node's kind or description. An empty query matches everything.
 */
export function filterCatalog(
  nodes: readonly NodeDescriptor[],
  query: string,
): NodeDescriptor[] {
  const terms = query.toLowerCase().split(/\s+/).filter((t) => t.length > 0);
  return nodes.filter((n) => {
    const haystack = (n.kind + " " + n.description).toLowerCase();
    return terms.every((t) => haystack.includes(t));
  });
}


export interface CatalogGroup {
  /** The pack name, e.g. "table". */
  pack: string;
  /** Total nodes in this pack, regardless of the current filter. */
  count: number;
  /** Nodes to render for this pack: all of them when collapsed or unfiltered-and-open, matches only while filtering. */
  nodes: NodeDescriptor[];
  /** Whether the pack's row should render expanded. */
  open: boolean;
}

/**
 * Groups a catalog by pack for the collapsed-tree sidebar. With no query, a
 * pack is open exactly when it is in `expanded` (the user's own choice,
 * persisted separately). With a query, every pack that has a match opens and
 * shows only its matching nodes; packs with no match are dropped, and
 * `expanded` is left untouched so clearing the query restores it.
 */
export function groupCatalog(
  nodes: readonly NodeDescriptor[],
  query: string,
  expanded: ReadonlySet<string>,
): CatalogGroup[] {
  const filtering = query.trim().length > 0;
  const allByPack = new Map<string, NodeDescriptor[]>();
  for (const n of nodes) {
    const pack = nodePack(n.kind);
    (allByPack.get(pack) ?? allByPack.set(pack, []).get(pack)!).push(n);
  }
  const visibleByPack = new Map<string, NodeDescriptor[]>();
  if (filtering) {
    for (const n of filterCatalog(nodes, query)) {
      const pack = nodePack(n.kind);
      (visibleByPack.get(pack) ?? visibleByPack.set(pack, []).get(pack)!).push(n);
    }
  }
  const packs = [...(filtering ? visibleByPack.keys() : allByPack.keys())].sort((a, b) =>
    a.localeCompare(b),
  );
  return packs.map((pack) => {
    const all = allByPack.get(pack) ?? [];
    const visible = filtering ? (visibleByPack.get(pack) ?? []) : all;
    return {
      pack,
      count: all.length,
      nodes: [...visible].sort((a, b) => a.kind.localeCompare(b.kind)),
      open: filtering ? true : expanded.has(pack),
    };
  });
}

const EXPANDED_PACKS_PREF_KEY = "bof-app-catalog-expanded";

/** Loads the set of pack names the user has expanded, persisted across reloads. */
export function loadExpandedPacks(): Set<string> {
  const raw = readPref(EXPANDED_PACKS_PREF_KEY);
  if (raw === null) return new Set();
  try {
    const parsed: unknown = JSON.parse(raw);
    return Array.isArray(parsed) ? new Set(parsed.filter((p): p is string => typeof p === "string")) : new Set();
  } catch {
    return new Set();
  }
}

/** Persists the set of pack names the user has expanded. */
export function saveExpandedPacks(packs: ReadonlySet<string>): void {
  writePref(EXPANDED_PACKS_PREF_KEY, JSON.stringify([...packs].sort()));
}
