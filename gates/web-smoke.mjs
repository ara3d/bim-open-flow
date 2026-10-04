// Headless smoke: test and typecheck every web package, then build the generic editor.
// Usage: node gates/web-smoke.mjs   (from the repo root; needs node deps.mjs and npm ci in bimopenflow/web)
import { spawnSync } from "node:child_process";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const web = join(root, "bimopenflow", "web");

const packages = ["api-client", "viz", "state", "panes", "client", "graph", "app"];
const steps = [
  ...packages.flatMap((p) => [["test", "-w", `@bimopenflow/${p}`], ["run", "typecheck", "-w", `@bimopenflow/${p}`]]),
  ["run", "build", "-w", "@bimopenflow/app"],
];

let failed = false;
for (const args of steps) {
  console.log(`\n== npm ${args.join(" ")} ==`);
  const res = spawnSync("npm", args, { cwd: web, stdio: "inherit", shell: true });
  if (res.status !== 0) { failed = true; console.error(`FAILED: npm ${args.join(" ")}`); }
}
console.log(failed ? "\nWEB SMOKE: FAIL" : "\nWEB SMOKE: PASS");
process.exitCode = failed ? 1 : 0;
