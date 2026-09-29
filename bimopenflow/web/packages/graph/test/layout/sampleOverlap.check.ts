// @vitest-environment node
// TKT-110: lists every committed sample graph whose cards overlap as the page
// that loads it shows them (stored positions, or autoLayout for a DuckDB
// workflow list), measured by the box each card paints in its largest
// style (nodeRender.nodeFootprint). Outside the default suite on purpose: run
// it with `npm run test:layout` in this package, against a running host for
// the node catalog (BOF_HOST, default http://127.0.0.1:5214). A failure is a
// warning to relay out the samples (scripts/relayout-samples.ts), not a gate.

import { expect, it } from "vitest";
import { fetchCatalog, overlappingPairs, sampleGraphs, shownModel, unknownKinds } from "../../scripts/sampleGraphs.js";

const host = process.env["BOF_HOST"] ?? "http://127.0.0.1:5214";

it("no sample graph has overlapping node cards", async () => {
  const catalog = await fetchCatalog(host);
  const report: string[] = [];
  for (const sample of sampleGraphs()) {
    const unknown = unknownKinds(sample.document, catalog);
    if (unknown.length) report.push(`${sample.name}: kinds missing from ${host}'s catalog: ${unknown.join(", ")}`);
    const pairs = overlappingPairs(shownModel(sample, catalog));
    if (pairs.length) report.push(`${sample.name}: ${pairs.length} overlapping pair(s): ${pairs.join(", ")}`);
  }
  if (report.length) console.log(report.join("\n"));
  expect(report).toEqual([]);
});
