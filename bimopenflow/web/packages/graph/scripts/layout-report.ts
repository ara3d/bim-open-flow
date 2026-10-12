// Prints the layout and wiring faults of every committed sample graph (layoutReport.ts).
//
//   npm run layout-report -- [--root dir] [--catalog file] [--samples dir,dir]
//
// Options as relayout-samples.ts: by default this repository's own samples
// against the generic catalog; the toolkit passes its root, docs/nodes.catalog.json,
// and its sample folders.

import { join } from "node:path";
import { layoutReport } from "./layoutReport.js";
import { committedCatalog, FLOW_SAMPLE_DIRS, repoRoot, sampleGraphs } from "./sampleGraphs.js";

const args = process.argv.slice(2);
const option = (name: string) => { const i = args.indexOf(name); return i >= 0 ? args[i + 1] : undefined; };
const root = option("--root") ?? repoRoot;
const catalogFile = option("--catalog");
const catalog = committedCatalog(catalogFile === undefined ? undefined : join(root, catalogFile));
const samples = sampleGraphs(option("--samples")?.split(",") ?? FLOW_SAMPLE_DIRS, root);
const report = layoutReport(samples, catalog);
for (const line of report) console.log(line);
console.log(`${report.length} of ${samples.length} sample graphs have layout or wiring faults`);
