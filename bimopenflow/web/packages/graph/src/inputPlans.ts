// The store actions behind the graph-input gestures (TKT-204): a source's path
// becomes a graph input ("Make a graph input"), and a graph input goes back
// into the source as a literal ("Inline the graph input"). Each plan is a
// list of actions to dispatch as one `batch` (one undo step), or an error.

import type { NodeDescriptor } from "@bimopenflow/contracts";
import { referencedInput, type Action, type State } from "@bimopenflow/state";
import { firstFree } from "./ids.js";
import { inputUses, inputValue } from "./graphInputs.js";
import { isSourceDescriptor } from "./sources.js";

type Catalog = ReadonlyMap<string, NodeDescriptor>;

/** The parameter every path-holding source stores its path in. */
export const PATH_PARAM = "path";

export type InputPlan = { readonly error: string } | { readonly actions: Action[] };

/** An input name from a node id: letters and digits only, starting with a letter. */
export function inputNameFor(nodeId: string, taken: Iterable<string>): string {
  const alnum = nodeId.replace(/[^A-Za-z0-9]/g, "");
  const base = /^[A-Za-z]/.test(alnum) ? alnum : `input${alnum}`;
  const names = new Set(taken);
  return names.has(base) ? firstFree((n) => `${base}${n + 1}`, names) : base;
}

function sourcePathSpec(state: State, catalog: Catalog, nodeId: string) {
  const kind = state.document.structure.nodes.find((n) => n.id === nodeId)?.kind ?? "";
  const desc = catalog.get(kind);
  const spec = desc?.params.find((p) => p.name === PATH_PARAM);
  return isSourceDescriptor(desc) && spec?.kind === "FilePath" ? spec : undefined;
}

/** The declared input parameter `param` of `nodeId` refers to, or null. */
export function inputOfParam(state: State, nodeId: string, param: string): string | null {
  const name = referencedInput(state.document.values[nodeId]?.[param] ?? "");
  return name !== null && (state.document.structure.inputs ?? []).some((i) => i.name === name) ? name : null;
}

/** True when `nodeId` is a source whose path holds a literal that could become an input. */
export function canMakeInput(state: State, catalog: Catalog, nodeId: string): boolean {
  const spec = sourcePathSpec(state, catalog, nodeId);
  if (!spec) return false;
  const value = state.document.values[nodeId]?.[PATH_PARAM] ?? spec.default;
  return value.trim() !== "" && referencedInput(value) === null;
}

/** True when `nodeId` is a source whose path is `$name` of a declared input. */
export const canInlineInput = (state: State, catalog: Catalog, nodeId: string): boolean =>
  sourcePathSpec(state, catalog, nodeId) !== undefined && inputOfParam(state, nodeId, PATH_PARAM) !== null;

/** "Make a graph input" on source `nodeId`: an input named after the node,
 *  holding the path as its default, and the path set to refer to it. */
export function makeInputPlan(state: State, catalog: Catalog, nodeId: string): InputPlan {
  const spec = sourcePathSpec(state, catalog, nodeId);
  if (!spec) return { error: `${nodeId} has no path to make a graph input of` };
  const path = state.document.values[nodeId]?.[PATH_PARAM] ?? spec.default;
  if (path.trim() === "") return { error: `${nodeId} holds no path to make a graph input of` };
  if (referencedInput(path) !== null) return { error: `${nodeId} already takes its path from a graph input` };
  const name = inputNameFor(nodeId, (state.document.structure.inputs ?? []).map((i) => i.name));
  return {
    actions: [
      { type: "declareInput", input: { name, kind: spec.kind, default: path } },
      { type: "setParam", nodeId, name: PATH_PARAM, value: `$${name}` },
    ],
  };
}

/** Puts the current value of the input that parameter `param` of `nodeId`
 *  refers to there as a literal, and removes the input when no other
 *  parameter refers to it. */
export function inlineInputPlan(state: State, nodeId: string, param: string): InputPlan {
  const name = inputOfParam(state, nodeId, param);
  const decl = (state.document.structure.inputs ?? []).find((i) => i.name === name);
  if (!decl) return { error: `${nodeId} ${param} does not refer to a graph input` };
  const others = inputUses(state).filter((u) => u.input === decl.name && !(u.nodeId === nodeId && u.param === param));
  return {
    actions: [
      { type: "setParam", nodeId, name: param, value: inputValue(state, decl) },
      ...(others.length === 0 ? [{ type: "removeInput", name: decl.name } as const] : []),
    ],
  };
}
