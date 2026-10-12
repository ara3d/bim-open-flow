// Lookups over the static schema pass held in State.schemas (TKT-209).
// Absence is normal: a host that predates the pass, or an output the pass does
// not cover, yields undefined and callers behave as they did before the pass.

import type { PortSchema } from "@bimopenflow/contracts";
import type { State } from "./reducer.js";

/** The schema of an output endpoint ("nodeId.output"). */
export function schemaOf(state: Pick<State, "schemas">, endpoint: string): PortSchema | undefined {
  return state.schemas[endpoint];
}

/** The schema of whatever feeds an input port ("nodeId.input"): the output at the other end of its wire. */
export function schemaFeeding(
  state: Pick<State, "schemas" | "document">,
  inputRef: string,
): PortSchema | undefined {
  const edge = state.document.structure.edges.find((e) => e.to === inputRef);
  return edge ? schemaOf(state, edge.from) : undefined;
}

/** The distinct errors and warnings the pass reports for a node's outputs, all
 *  worded as warnings by the host; errors first. Never blocks anything. */
export function schemaMessages(state: Pick<State, "schemas">, nodeId: string): string[] {
  const prefix = `${nodeId}.`;
  const ports = Object.entries(state.schemas).filter(([key]) => key.startsWith(prefix)).map(([, p]) => p);
  return [...new Set([...ports.flatMap((p) => p.errors), ...ports.flatMap((p) => p.warnings)])];
}
