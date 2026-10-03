// @vitest-environment node
// The package boundary, enforced: the client library sits below the editor
// and the notebook, so nothing under src imports either of them or reaches
// into another package's files by a relative path; and the "/host" entry
// reaches no pane or viewer code.

import { readdirSync, readFileSync } from "node:fs";
import { join, relative } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const src = fileURLToPath(new URL("../src", import.meta.url));

const forbidden = ["@bimopenflow/app", "@bimopenflow/bim-open-notebook", "deps/gratify"];


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
  it("imports neither application nor another package's files", () => {
    const offences: string[] = [];
    for (const file of sourceFiles(src)) {
      for (const spec of specifiers(readFileSync(file, "utf8"))) {
        const offence = `${relative(src, file).split("\\").join("/")} -> ${spec}`;
        if (spec.startsWith("..") || forbidden.some((f) => spec.includes(f)))
          offences.push(offence);
      }
    }
    expect(offences).toEqual([]);
  });

  it("keeps the host entry free of pane and viewer code", () => {
    // Walks the relative imports from host.ts; entry pages load it before the panes.
    const heavy = ["@bimopenflow/panes", "@bim-open-viewer", "three"];
    const seen = new Set<string>();
    const offences: string[] = [];
    const visit = (name: string) => {
      if (seen.has(name)) return;
      seen.add(name);
      for (const spec of specifiers(readFileSync(join(src, `${name}.ts`), "utf8"))) {
        if (spec.startsWith("./")) visit(spec.slice(2).replace(/\.js$/, ""));
        else if (heavy.some((h) => spec === h || spec.startsWith(`${h}/`))) offences.push(`${name}.ts -> ${spec}`);
      }
    };
    visit("host");
    expect(seen.size).toBeGreaterThan(1);
    expect(offences).toEqual([]);
  });
});
