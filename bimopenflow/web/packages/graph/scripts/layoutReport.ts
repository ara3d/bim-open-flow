// The layout and wiring faults of every committed sample graph (layoutCheck.ts),
// shared by the sample layout tests here and in the toolkit; layout-report.ts
// prints it.
//
// Each graph is checked twice, as the studio draws it (sources in the rail,
// their wires as chips) and as a plain canvas draws it (every wire drawn), at
// its stored positions; a workflow-list entry is checked as autoLayout places
// it, since that is how the DuckDB studio opens it.

import type { NodeDescriptor } from "@bimopenflow/contracts";
import { initialState } from "@bimopenflow/state";
import { autoLayout } from "../src/autoLayout.js";
import { connectionIssues, layoutIssues, type LayoutIssue } from "../src/layoutCheck.js";
import { NO_PORT_RESULTS } from "../src/portResults.js";
import { DEFAULT_SOURCE_VIEW, NO_SOURCE_VIEW, type SourceView } from "../src/sources.js";
import { buildCanvasModel, type CanvasModel } from "../src/viewModel.js";
import type { SampleGraph } from "./sampleGraphs.js";

/** The canvas model of `sample` drawn with `sources`, laid out as its page
 *  shows it: a workflow-list entry is laid out as the app's layoutAll does
 *  (autoLayout over the plain model, saved into the layout), then drawn. */
export function drawnModel(sample: SampleGraph, catalog: ReadonlyMap<string, NodeDescriptor>, sources: SourceView): CanvasModel {
  const build = (document: SampleGraph["document"]) =>
    buildCanvasModel({ ...initialState, document }, catalog, null, NO_PORT_RESULTS, { sources });
  if (!sample.autoLaidOut) return build(sample.document);
  const at = autoLayout(buildCanvasModel({ ...initialState, document: sample.document }, catalog, null));
  return build({ ...sample.document, layout: { ...sample.document.layout, ...at } });
}

/** Furthest a stored position may sit from autoLayout's: the relayout script rounds to whole pixels. */
const PLACED_SLACK = 1;

/** The nodes of `sample` whose stored position is not where autoLayout puts them. Sample positions come from
 *  the layout tool (relayout-samples), never from a hand: a hand grid drifts from the card sizes the editor
 *  draws (TKT-259 placed rows 184 px apart for cards up to 500 px tall). A workflow-list entry is laid out
 *  when it opens, so its stored positions do not count. */
export function handPlaced(sample: SampleGraph, catalog: ReadonlyMap<string, NodeDescriptor>): LayoutIssue[] {
  if (sample.autoLaidOut) return [];
  const at = autoLayout(buildCanvasModel({ ...initialState, document: sample.document }, catalog, null));
  return Object.entries(at).flatMap(([id, want]) => {
    const stored = sample.document.layout[id];
    const off = !stored || Math.abs(stored.x - want.x) > PLACED_SLACK || Math.abs(stored.y - want.y) > PLACED_SLACK;
    return off ? [{ kind: "handPlaced" as const, text: `${id} at ${stored ? `${stored.x}, ${stored.y}` : "no position"}, layout puts it at ${Math.round(want.x)}, ${Math.round(want.y)}` }] : [];
  });
}

/** Every fault of `sample`, each named once, as "kind: text". */
export function sampleIssues(sample: SampleGraph, catalog: ReadonlyMap<string, NodeDescriptor>): string[] {
  const issues: LayoutIssue[] = [
    ...layoutIssues(drawnModel(sample, catalog, DEFAULT_SOURCE_VIEW)),
    ...layoutIssues(drawnModel(sample, catalog, NO_SOURCE_VIEW)),
    ...connectionIssues(sample.document, catalog),
    ...handPlaced(sample, catalog),
  ];
  return [...new Set(issues.map((i) => `${i.kind}: ${i.text}`))];
}

/** One line per sample with faults: "name: fault; fault". */
export const layoutReport = (samples: readonly SampleGraph[], catalog: ReadonlyMap<string, NodeDescriptor>): string[] =>
  samples.flatMap((s) => {
    const issues = sampleIssues(s, catalog);
    return issues.length ? [`${s.name}: ${issues.join("; ")}`] : [];
  });
