// @vitest-environment node
// The sample graphs over public buildings (samples/buildings, over bim-open-data's
// samples/public) open in the editor as cleanly as the table samples: every kind is
// in the generic catalog, no two cards overlap, and no card text overflows. They are a separate folder from FLOW_SAMPLE_DIRS because the host does
// not seed them. On an overlap, run
// `npm run relayout-samples -- --samples samples/buildings`.

import { expect, it } from "vitest";
import { sampleOverflows } from "../scripts/cardOverflow.js";
import { committedCatalog, overlappingPairs, sampleGraphs, shownModel, unknownKinds } from "../scripts/sampleGraphs.js";

const catalog = committedCatalog();
const samples = sampleGraphs(["samples/buildings"]);

it("finds the six building sample graphs", () => {
  expect(samples.map((s) => s.name)).toHaveLength(6);
});

it("every kind a building sample uses is in the committed catalog", () => {
  expect(samples.flatMap((s) => unknownKinds(s.document, catalog).map((k) => `${s.name}: ${k}`))).toEqual([]);
});

it("no building sample has overlapping node cards", () => {
  const report = samples.flatMap((sample) => {
    const pairs = overlappingPairs(shownModel(sample, catalog));
    return pairs.length ? [`${sample.name}: ${pairs.join(", ")}`] : [];
  });
  expect(report).toEqual([]);
});

it("no text overflows a building sample's cards", () => {
  expect(sampleOverflows(samples, catalog)).toEqual([]);
});
