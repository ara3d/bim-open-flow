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

/** Every fault of `sample`, each named once, as "kind: text". */
export function sampleIssues(sample: SampleGraph, catalog: ReadonlyMap<string, NodeDescriptor>): string[] {
  const issues: LayoutIssue[] = [
    ...layoutIssues(drawnModel(sample, catalog, DEFAULT_SOURCE_VIEW)),
    ...layoutIssues(drawnModel(sample, catalog, NO_SOURCE_VIEW)),
    ...connectionIssues(sample.document, catalog),
  ];
  return [...new Set(issues.map((i) => `${i.kind}: ${i.text}`))];
}

/** One line per sample with faults: "name: fault; fault". */
export const layoutReport = (samples: readonly SampleGraph[], catalog: ReadonlyMap<string, NodeDescriptor>): string[] =>
  samples.flatMap((s) => {
    const issues = sampleIssues(s, catalog);
    return issues.length ? [`${s.name}: ${issues.join("; ")}`] : [];
  });
