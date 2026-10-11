// The store actions behind the source gestures (TKT-138): a dropped file
// becomes a source, an inline path is promoted to one, and the other nodes
// holding the same path are offered the link. Pure: each plan is a list of
// actions to dispatch as one `batch` (one undo step), or an error to show.

import type { NodeDescriptor, PortType } from "@bimopenflow/contracts";
import type { Action, State } from "@bimopenflow/state";
import { fileName } from "./paramText.js";
import { freshNodeId } from "./ids.js";
import { findSourceByPath, pathSourceKind, samePath, type SourceMatch } from "./sources.js";

type Catalog = ReadonlyMap<string, NodeDescriptor>;

/** The parameter every path-holding source stores its path in. */
const PATH_PARAM = "path";

/** The socket type a dropped file of this extension becomes a source of, or
 *  undefined for a file the toolkit does not read. A DuckDB file is a
 *  Database; a BOS or IFC file travels as a path (Text). */
export function dropSourceType(path: string): PortType | undefined {
  const ext = fileName(path).toLowerCase().split(".").pop();
  if (ext === "duckdb") return "Database";
  return ext === "bos" || ext === "ifc" ? "Text" : undefined;
}

/** A node parameter that could take the link, and holds the path. */
export interface RelinkTarget {
  readonly nodeId: string;
  readonly param: string;
}

export type SourcePlan =
  | { readonly error: string }
  | {
    readonly actions: Action[];
    /** The source the gesture ends with (new or already there). */
    readonly source: SourceMatch;
    /** Other parameters holding the same path that are not yet linked. */
    readonly relink: RelinkTarget[];
  };

const valueOf = (state: State, catalog: Catalog, nodeId: string, param: string): string => {
  const kind = state.document.structure.nodes.find((n) => n.id === nodeId)?.kind ?? "";
  return state.document.values[nodeId]?.[param] ?? catalog.get(kind)?.params.find((p) => p.name === param)?.default ?? "";
};

const isWired = (state: State, nodeId: string, param: string): boolean =>
  state.document.structure.edges.some((e) => e.to === `${nodeId}.${param}`);

/** Parameters named `param`, bindable as `type`, holding `path`, wired to nothing, on nodes other than `except`. */
export function relinkTargets(
  state: State,
  catalog: Catalog,
  path: string,
  param: string,
  type: PortType,
  except: string,
): RelinkTarget[] {
  return state.document.structure.nodes.flatMap((n) => {
    const spec = catalog.get(n.kind)?.params.find((p) => p.name === param);
    return n.id !== except && spec?.bindable === type && !isWired(state, n.id, param)
      && samePath(valueOf(state, catalog, n.id, param), path)
      ? [{ nodeId: n.id, param }] : [];
  });
}

/** Actions that add a source of kind `desc` holding `path`, returning its id. */
function addSource(state: State, desc: NodeDescriptor, path: string, at?: { x: number; y: number }): { id: string; actions: Action[] } {
  const id = freshNodeId(desc.kind, state.document.structure.nodes.map((n) => n.id));
  return {
    id,
    actions: [
      { type: "addNode", id, kind: desc.kind, version: desc.version },
      { type: "setParam", nodeId: id, name: PATH_PARAM, value: path },
      ...(at ? [{ type: "setLayout", nodeId: id, layout: at } as const] : []),
    ],
  };
}

/** A file dropped on the canvas at world point `at`: a source holding its
 *  path, or, when one already holds it, that source selected. */
export function dropSourcePlan(state: State, catalog: Catalog, path: string, at: { x: number; y: number }): SourcePlan {
  const type = dropSourceType(path);
  if (!type) return { error: `${fileName(path)} is not a .duckdb, .bos or .ifc file` };
  const existing = findSourceByPath(state.document, catalog, path, type);
  if (existing) return { actions: [{ type: "select", ids: [existing.id] }], source: existing, relink: [] };
  const desc = pathSourceKind(catalog, type);
  if (!desc) return { error: `The catalog has no source node for ${type} paths` };
  const { id, actions } = addSource(state, desc, path, at);
  const output = desc.outputs[0]!.name;
  return { actions: [...actions, { type: "select", ids: [id] }], source: { id, endpoint: `${id}.${output}` }, relink: [] };
}

/** "Share as source" on the path parameter `param` of node `nodeId`: a source
 *  holding that path (the one already there, else a new one), this parameter
 *  wired to it, and the other parameters to offer the link to. */
export function promotePlan(state: State, catalog: Catalog, nodeId: string, param: string): SourcePlan {
  const kind = state.document.structure.nodes.find((n) => n.id === nodeId)?.kind;
  const spec = catalog.get(kind ?? "")?.params.find((p) => p.name === param);
  if (!spec?.bindable || spec.kind !== "FilePath") return { error: `${nodeId} ${param} cannot take a source` };
  const path = valueOf(state, catalog, nodeId, param);
  if (path.trim() === "") return { error: `${nodeId} ${param} holds no path to share` };
  if (isWired(state, nodeId, param)) return { error: `${nodeId} ${param} is already linked to a source` };
  const type = spec.bindable;
  let source = findSourceByPath(state.document, catalog, path, type, nodeId);
  let actions: Action[] = [];
  if (!source) {
    const desc = pathSourceKind(catalog, type);
    if (!desc) return { error: `The catalog has no source node for ${type} paths` };
    const added = addSource(state, desc, path);
    source = { id: added.id, endpoint: `${added.id}.${desc.outputs[0]!.name}` };
    actions = added.actions;
  }
  return {
    actions: [...actions, { type: "connect", from: source.endpoint, to: `${nodeId}.${param}` }],
    source,
    relink: relinkTargets(state, catalog, path, param, type, nodeId),
  };
}

/** The actions that wire `source` into every target. */
export const relinkActions = (source: SourceMatch, targets: readonly RelinkTarget[]): Action[] =>
  targets.map((t) => ({ type: "connect", from: source.endpoint, to: `${t.nodeId}.${t.param}` }));
