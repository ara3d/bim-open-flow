// @vitest-environment node
// TKT-286: every sample graph of the graph tool's own (FLOW_SAMPLE_DIRS) passes
// the layout and wiring check (layoutCheck.ts) as the studio and a plain canvas
// draw it: no overlapping cards, no wire behind a card, no wire running
// backwards, no unwired input, no node with no wire, and every position where
// autoLayout puts it (positions come from the tool, never from a hand). On a failure, relay out
// with `npm run relayout-samples`, or list the faults with `npm run layout-report`.

import { expect, it } from "vitest";
import { handPlaced, layoutReport } from "../scripts/layoutReport.js";
import { committedCatalog, sampleGraphs } from "../scripts/sampleGraphs.js";

it("no sample graph has a layout or wiring fault", () => {
  expect(layoutReport(sampleGraphs(), committedCatalog())).toEqual([]);
});

it("names a node moved by hand", () => {
  const sample = sampleGraphs().find((s) => !s.autoLaidOut)!;
  const [id, at] = Object.entries(sample.document.layout)[0]!;
  const moved = { ...sample, document: { ...sample.document, layout: { ...sample.document.layout, [id]: { ...at, x: at.x + 50 } } } };
  expect(handPlaced(sample, committedCatalog())).toEqual([]);
  expect(handPlaced(moved, committedCatalog()).map((i) => i.text)).toEqual([expect.stringContaining(`${id} at ${at.x + 50}`)]);
});
