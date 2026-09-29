// Relays out the committed sample graphs whose cards overlap (TKT-110), with
// autoLayout.tidyLayout: columns and reading order stay as stored, cards move
// right or down only as far as their painted footprints need.
//
//   npm run relayout-samples -- [--host http://127.0.0.1:5214] [--dry] [path-substring ...]
//
// Sizes cards from the committed docs/nodes.catalog.json, or from a running
// host's catalog with --host (or BOF_HOST). Rewrites only the x and y
// numbers of the layout entries, in place, so each file keeps its formatting;
// the graph hash ignores layout, so golden files and run records stay valid.
// A file with uncommitted changes is skipped, since another session owns it.
// A node with no stored position gets an entry at the start of the block.

import { execFileSync } from "node:child_process";
import { readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { tidyLayout } from "../src/autoLayout.js";
import { committedCatalog, fetchCatalog, overlappingPairs, repoRoot, sampleGraphs, sampleModel } from "./sampleGraphs.js";

const args = process.argv.slice(2);
const option = (name: string) => {
  const i = args.indexOf(name);
  return i >= 0 ? args.splice(i, 2)[1] : undefined;
};
const host = option("--host") ?? process.env["BOF_HOST"];
const dry = args.includes("--dry");
const filters = args.filter((a) => a !== "--dry");

const escape = (s: string) => s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");

/** `text` with the stored x and y of `id` in the layout block replaced by
 *  `to`, or a new entry when it has none. */
function setPosition(text: string, id: string, to: { x: number; y: number }): string {
  const layout = text.indexOf('"layout"');
  if (layout < 0) throw new Error("no layout block");
  const pattern = new RegExp(`("${escape(id)}"\\s*:\\s*\\{\\s*"x"\\s*:\\s*)-?[\\d.]+(\\s*,\\s*"y"\\s*:\\s*)-?[\\d.]+`, "g");
  pattern.lastIndex = layout;
  const match = pattern.exec(text);
  if (!match) {
    // A node placed by defaultPosition: a new first entry, indented as the
    // entry after it.
    const open = text.indexOf("{", layout) + 1;
    const indent = /^\r?\n[ \t]*/.exec(text.slice(open))?.[0] ?? " ";
    return `${text.slice(0, open)}${indent}"${id}": { "x": ${Math.round(to.x)}, "y": ${Math.round(to.y)} },${text.slice(open)}`;
  }
  const replaced = `${match[1]}${Math.round(to.x)}${match[2]}${Math.round(to.y)}`;
  return text.slice(0, match.index) + replaced + text.slice(match.index + match[0].length);
}

const dirty = new Set(
  execFileSync("git", ["status", "--porcelain", "--", "samples"], { cwd: repoRoot, encoding: "utf8" })
    .split("\n")
    .filter(Boolean)
    .map((line) => line.slice(3)),
);

const catalog = host === undefined ? committedCatalog() : await fetchCatalog(host);
const texts = new Map<string, string>();
// Workflow lists (samples/duckdb-analyses/workflows.json) are skipped: the
// DuckDB studio lays each graph out afresh with autoLayout when it opens it.
for (const sample of sampleGraphs()) {
  if (filters.length && !filters.some((f) => sample.name.includes(f))) continue;
  const model = sampleModel(sample.document, catalog);
  const before = overlappingPairs(model);
  if (!before.length || sample.autoLaidOut) continue;
  if (dirty.has(sample.file)) {
    console.log(`skip ${sample.name}: uncommitted changes (${before.length} overlapping pairs)`);
    continue;
  }
  const positions = tidyLayout(model);
  const after = overlappingPairs({ ...model, nodes: model.nodes.map((n) => ({ ...n, ...positions[n.id]! })) });
  let text = readFileSync(join(repoRoot, sample.file), "utf8");
  for (const node of model.nodes) text = setPosition(text, node.id, positions[node.id]!);
  texts.set(sample.file, text);
  console.log(`${sample.name}: ${before.length} overlapping pairs -> ${after.length}`);
}
if (!dry) for (const [file, text] of texts) writeFileSync(join(repoRoot, file), text);
console.log(`${dry ? "would rewrite" : "rewrote"} ${texts.size} file(s)`);
