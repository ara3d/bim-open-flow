// Graph inputs on the canvas (TKT-204): the cards the sources rail holds for
// them, and how a `$name` parameter value is read. The document holds only
// the declarations; the current values come from the host's last evaluation
// update (State.graphInputs). Pure and gratify-free.

import type { NodeDescriptor, ParamKind, PortType, SuggestDescriptor } from "@bimopenflow/contracts";
import { referencedInput, type GraphInputDecl, type State } from "@bimopenflow/state";
import type { CanvasParam } from "./canvasSlots.js";

type Catalog = ReadonlyMap<string, NodeDescriptor>;

/** The `kind` of a card drawn for a graph input. It names no catalog entry:
 *  an input is not a node, and the document never holds one with this kind. */
export const INPUT_KIND = "graph.input";

const PREFIX = "$";

/** The one output port of an input's card: the wire from it to a parameter
 *  holding `$name` is the reference, drawn (2026-10-12: a `$name` in a text
 *  box with no wire read as an unconnected card, as chips had). */
export const INPUT_PORT = "value";

/** The port type an input's output socket shows: its kind where that is a
 *  port type, else Text (a file path travels as text). The type is for the
 *  label; canConnect matches a graph input by kind, not type. */
export const inputPortType = (kind: ParamKind): PortType =>
  kind === "Boolean" || kind === "Integer" || kind === "Number" ? kind : "Text";

/** The card id of input `name`. Node ids of the document may not collide with
 *  it in practice, and the model marks the card (`CanvasNode.input`) anyway. */
export const inputCardId = (name: string): string => PREFIX + name;

/** What a card marked as a graph input carries. */
export interface InputMark {
  readonly name: string;
  readonly kind: ParamKind;
}

/** The value an input has now: the host's, else the declared default. */
export function inputValue(state: State, decl: GraphInputDecl): string {
  return state.graphInputs.find((i) => i.name === decl.name)?.value ?? decl.default;
}

/** Every (node, param) whose stored value is exactly `$name` for a declared
 *  input, with the input's name. */
export function inputUses(state: State): { nodeId: string; param: string; input: string }[] {
  const declared = new Set((state.document.structure.inputs ?? []).map((i) => i.name));
  return Object.entries(state.document.values).flatMap(([nodeId, params]) =>
    Object.entries(params).flatMap(([param, value]) => {
      const input = referencedInput(value);
      return input !== null && declared.has(input) ? [{ nodeId, param, input }] : [];
    }));
}

/** The suggestion descriptor and the (node, param) to ask the host for suggestions
 *  of input `name`: the first parameter that holds `$name` and has suggestions. */
export function inputSuggest(
  state: State,
  catalog: Catalog,
  name: string,
): { suggest: SuggestDescriptor; nodeId: string; param: string } | null {
  for (const use of inputUses(state)) {
    if (use.input !== name) continue;
    const kind = state.document.structure.nodes.find((n) => n.id === use.nodeId)?.kind ?? "";
    const suggest = catalog.get(kind)?.params.find((p) => p.name === use.param)?.suggest;
    if (suggest) return { suggest, nodeId: use.nodeId, param: use.param };
  }
  return null;
}

/** The one parameter row of input `decl`'s card: named after the input, with
 *  the kind of the input and the suggestions of the parameters that use it. */
export function inputParam(state: State, catalog: Catalog, decl: GraphInputDecl): CanvasParam {
  const suggest = inputSuggest(state, catalog, decl.name)?.suggest;
  return {
    name: decl.name,
    kind: decl.kind,
    value: inputValue(state, decl),
    ...(suggest ? { suggest } : {}),
  };
}

/** Marks the parameters whose value is `$name` of a declared input (and that
 *  no edge feeds) with the input's name and current value. */
export function markInputRefs(state: State, params: readonly CanvasParam[]): CanvasParam[] {
  const decls = state.document.structure.inputs ?? [];
  return params.map((p) => {
    const name = p.wired === undefined ? referencedInput(p.value) : null;
    const decl = name === null ? undefined : decls.find((d) => d.name === name);
    return decl ? { ...p, inputRef: { name: decl.name, value: inputValue(state, decl) } } : p;
  });
}
