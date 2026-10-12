// What the flow picker lists (TKT-253): the user's own flows apart from the
// seeded samples, and without the untitled flows New left empty. DOM-free;
// the chromes render the groups (fillFlowPicker in topbar.ts).

import type { AnalysisSummary } from "@bimopenflow/contracts";
import { isUntitledId } from "./ids.js";

export interface FlowGroup {
  /** The optgroup label; null when the page has no samples to set apart. */
  readonly label: string | null;
  readonly flows: readonly AnalysisSummary[];
}

export const OWN_FLOWS_LABEL = "Your flows";
export const SAMPLES_LABEL = "Samples";

/** The picker's groups: the user's flows first, then the samples (ids in
 *  `sampleIds`). `hidden` ids are left out unless open; an open flow the host
 *  does not list yet (an unstored new flow) is added to the user's flows. */
export function flowGroups(
  list: readonly AnalysisSummary[],
  sampleIds: ReadonlySet<string>,
  hidden: ReadonlySet<string>,
  activeId: string | null,
): FlowGroup[] {
  const visible = list.filter((a) => !hidden.has(a.id) || a.id === activeId);
  if (activeId !== null && !list.some((a) => a.id === activeId)) visible.unshift({ id: activeId, graphHash: "" });
  if (sampleIds.size === 0) return visible.length ? [{ label: null, flows: visible }] : [];
  const groups: FlowGroup[] = [
    { label: OWN_FLOWS_LABEL, flows: visible.filter((a) => !sampleIds.has(a.id)) },
    { label: SAMPLES_LABEL, flows: visible.filter((a) => sampleIds.has(a.id)) },
  ];
  return groups.filter((g) => g.flows.length > 0);
}

/** Finds the untitled flows that hold no nodes, the ones earlier presses of
 *  New left behind. Reads one document per distinct graph hash and
 *  remembers the answer, so every empty flow after the first costs nothing. */
export function emptyUntitledFinder(
  getAnalysis: (id: string) => Promise<string>,
): (list: readonly AnalysisSummary[]) => Promise<Set<string>> {
  const emptyByHash = new Map<string, Promise<boolean>>();
  const isEmpty = (a: AnalysisSummary): Promise<boolean> => {
    let known = emptyByHash.get(a.graphHash);
    if (!known) {
      known = getAnalysis(a.id).then(
        (json) => (JSON.parse(json)?.structure?.nodes ?? []).length === 0,
        () => false,
      );
      emptyByHash.set(a.graphHash, known);
    }
    return known;
  };
  return async (list) => {
    const untitled = list.filter((a) => isUntitledId(a.id));
    const empty = await Promise.all(untitled.map(isEmpty));
    return new Set(untitled.filter((_, i) => empty[i]).map((a) => a.id));
  };
}
