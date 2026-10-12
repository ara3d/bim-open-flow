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

const UNTITLED = /^untitled-\d+$/;

/** True for the id New gives a flow before the user names it (TKT-253). */
export const isUntitledId = (id: string | null | undefined): boolean => !!id && UNTITLED.test(id);

/** A name typed by a person as a host analysis id: a lowercase slug of
 *  a-z, 0-9, and interior hyphens (AnalysisId on the host); "" when nothing is left. */
export const slugId = (name: string): string =>
  name.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/^-+|-+$/g, "");
