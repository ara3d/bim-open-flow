// @vitest-environment node
// The package boundary, enforced: nothing under src imports the application,
// the panes, the host client, the notebook, or gratify's example code. A
// second page can then mount the editor without pulling the studio along.

import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const src = fileURLToPath(new URL("../src", import.meta.url));

const forbidden = [
  "@bimopenflow/app",
  "@bimopenflow/panes",
  "@bimopenflow/api-client",
  "@bimopenflow/bim-open-notebook",
  "@bimopenflow/viz",
  "deps/gratify/examples",
];

/** Every import or export specifier in a TypeScript source, static or dynamic. */
function specifiers(source: string): string[] {
  const out: string[] = [];
  for (const m of source.matchAll(/\b(?:from|import)\s*\(?\s*["']([^"']+)["']/g)) out.push(m[1]!);
  return out;
}

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) =>
    entry.isDirectory() ? sourceFiles(join(dir, entry.name))
      : entry.name.endsWith(".ts") ? [join(dir, entry.name)] : []);
}

describe("layering", () => {
  it("imports nothing from the application layer or gratify's examples", () => {
    const offences: string[] = [];
    for (const file of sourceFiles(src)) {
      for (const spec of specifiers(readFileSync(file, "utf8"))) {
        const leavesSrc = spec.startsWith("..");
        if (leavesSrc || forbidden.some((f) => spec.includes(f)))
          offences.push(`${file.slice(src.length + 1)} -> ${spec}`);
      }
    }
    expect(offences).toEqual([]);
  });
});
