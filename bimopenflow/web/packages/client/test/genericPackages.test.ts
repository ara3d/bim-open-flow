// @vitest-environment node
// The editor's generic packages never depend on the BIM Open viewer or on the
// 3D pane, so they can move to their own repository (repository split, phase
// 5) without the viewer: no package.json of theirs lists, and no source or
// test file of theirs imports, "@bim-open-viewer/*" or "@bimopenflow/pane-3d".
// The toolkit's pages (studio-web) register the 3D pane instead.

import { existsSync, readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

const packages = fileURLToPath(new URL("../..", import.meta.url));

const generic = ["contracts", "api-client", "state", "graph", "viz", "client", "panes", "app"];
const forbidden = ["@bim-open-viewer/", "@bimopenflow/pane-3d"];

function files(dir: string): string[] {
  if (!existsSync(dir)) return [];
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) =>
    entry.isDirectory() ? files(join(dir, entry.name))
      : /\.(ts|tsx|mts)$/.test(entry.name) ? [join(dir, entry.name)] : []);
}

/** Every import or export specifier in a TypeScript source, static or dynamic. */
const specifiers = (source: string): string[] =>
  [...source.matchAll(/\b(?:from|import)\s*\(?\s*["']([^"']+)["']/g)].map((m) => m[1]!);

/** Every package a package.json depends on, in any dependency section. */
const dependencies = (manifest: Record<string, unknown>): string[] =>
  ["dependencies", "devDependencies", "peerDependencies", "optionalDependencies"]
    .flatMap((section) => Object.keys((manifest[section] ?? {}) as object));

const named = (spec: string): boolean => forbidden.some((f) => spec === f || spec.startsWith(f.endsWith("/") ? f : `${f}/`));

describe("generic editor packages", () => {
  it.each(generic)("%s depends on neither the viewer nor the 3D pane", (name) => {
    const root = join(packages, name);
    const manifest = join(root, "package.json");
    expect(existsSync(manifest), `${name}/package.json`).toBe(true);
    const offences = dependencies(JSON.parse(readFileSync(manifest, "utf8")))
      .filter(named).map((dep) => `package.json: ${dep}`);
    // This file's own self-check below names both on purpose.
    const self = fileURLToPath(import.meta.url);
    for (const file of [...files(join(root, "src")), ...files(join(root, "test"))].filter((f) => f !== self))
      for (const spec of specifiers(readFileSync(file, "utf8")))
        if (named(spec)) offences.push(`${file.slice(root.length + 1).split("\\").join("/")}: ${spec}`);
    expect(offences).toEqual([]);
  });

  it("would catch a viewer import", () => {
    expect(specifiers(`import { x } from "@bim-open-viewer/model";`).some(named)).toBe(true);
    expect(specifiers(`const m = await import("@bimopenflow/pane-3d");`).some(named)).toBe(true);
    expect(specifiers(`import { x } from "@bimopenflow/panes";`).some(named)).toBe(false);
  });
});
