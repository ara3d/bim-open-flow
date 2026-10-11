import { firstFree, freshNodeId } from "@bimopenflow/graph";

export { freshNodeId };

/** The next free "untitled-N" analysis id. */
export function freshUntitledId(existing: Iterable<string>): string {
  return firstFree((n) => `untitled-${n}`, existing);
}

/** The id for a copy of `source`: "door-schedule-copy", then "-copy-2", ...
 *  so a flow started from a template keeps the template's name in view. */
export function freshCopyId(source: string, existing: Iterable<string>): string {
  return firstFree((n) => (n === 1 ? `${source}-copy` : `${source}-copy-${n}`), existing);
}
