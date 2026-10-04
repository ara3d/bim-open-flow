// @vitest-environment node
// TKT-110: no sample graph of the graph tool's own (FLOW_SAMPLE_DIRS: the
// folders that move with it to bim-open-flow) has overlapping cards as the
// page that loads it shows them, measured by the box each card paints in its
// largest style (nodeRender.nodeFootprint). Card sizes come from the generic
// host's committed node catalog, test/nodes.catalog.json, so no host is needed.
// On a failure, relay out the samples with `npm run relayout-samples`;
// notebooks that embed a graph then follow with bim-open-notebook's
// scripts/sync-embed-layouts.ts.

import { expect, it } from "vitest";
import { committedCatalog, overlappingPairs, sampleGraphs, shownModel, unknownKinds } from "../scripts/sampleGraphs.js";

const catalog = committedCatalog();

it("finds the graph tool's sample graphs", () => {
  // 6 in samples/analyses and 5 in samples/relations when this was written.
  expect(sampleGraphs().length).toBeGreaterThanOrEqual(11);
});

it("every kind a sample graph uses is in the committed catalog", () => {
  const unknown = sampleGraphs().flatMap((s) => unknownKinds(s.document, catalog).map((k) => `${s.name}: ${k}`));
  expect(unknown).toEqual([]);
});

it("no sample graph has overlapping node cards", () => {
  const report = sampleGraphs().flatMap((sample) => {
    const pairs = overlappingPairs(shownModel(sample, catalog));
    return pairs.length ? [`${sample.name}: ${pairs.join(", ")}`] : [];
  });
  expect(report).toEqual([]);
});
