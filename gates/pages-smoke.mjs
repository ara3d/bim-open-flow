// Pages smoke: tests and builds the live graphs of the GitHub Pages page
// (bimopenflow/web/packages/site-web), then checks what GitHub Pages will serve
// from site/: the built bundle exists, site/index.html links it and holds the
// element it mounts into, and the data file it fetches lists graphs. The
// site-web test walks every graph of site/data/buildings.json through the
// static API (analyses, document, a state with every node Ok, table pages,
// the catalog), so a graph the page cannot open fails here.
// Usage: node gates/pages-smoke.mjs   (from the repo root; needs node deps.mjs and npm ci in bimopenflow/web)
import { spawnSync } from "node:child_process";
import { existsSync, readFileSync, statSync } from "node:fs";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const web = join(root, "bimopenflow", "web");
const site = join(root, "site");

let failed = false;
const fail = (message) => { failed = true; console.error(`FAILED: ${message}`); };

for (const args of [["test", "-w", "@bimopenflow/site-web"], ["run", "typecheck", "-w", "@bimopenflow/site-web"], ["run", "build", "-w", "@bimopenflow/site-web"]]) {
  console.log(`\n== npm ${args.join(" ")} ==`);
  const res = spawnSync("npm", args, { cwd: web, stdio: "inherit", shell: true });
  if (res.status !== 0) fail(`npm ${args.join(" ")}`);
}

console.log("\n== site/ as Pages serves it ==");
for (const file of ["app/site.js", "app/site.css", "data/buildings.json", "data/NOTICE.md", "notice.js"]) {
  const path = join(site, file);
  if (!existsSync(path) || statSync(path).size === 0) fail(`site/${file} is missing or empty`);
}
const html = readFileSync(join(site, "index.html"), "utf8");
for (const needle of ['src="app/site.js"', 'href="app/site.css"', 'id="buildings"', 'href="data/NOTICE.md"'])
  if (!html.includes(needle)) fail(`site/index.html lacks ${needle}`);
const graphs = JSON.parse(readFileSync(join(site, "data", "buildings.json"), "utf8")).graphs ?? [];
if (graphs.length === 0) fail("site/data/buildings.json lists no graphs");
else console.log(`${graphs.length} graphs: ${graphs.map((g) => g.id).join(", ")}`);

console.log(failed ? "\nPAGES SMOKE: FAIL" : "\nPAGES SMOKE: PASS");
process.exitCode = failed ? 1 : 0;
