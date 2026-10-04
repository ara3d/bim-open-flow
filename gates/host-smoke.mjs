// Headless smoke: start the real host, drive the HTTP API end to end, shut down.
// The generic host (bimopenflow-host, profile "tables") must serve the table packs and no
// BIM pack; bim-open-toolkit's own gate does the same for its studio.
// Usage: node gates/host-smoke.mjs   (from the repo root; needs dotnet + buildable hosts)
import { spawn } from "node:child_process";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");

const HOSTS = [
  {
    name: "host",
    project: join(root, "src", "flow", "BimOpenFlow.Host"),
    expect: ["table.inline", "table.sort", "rel.csv", "sink.exportCsv"],
    forbid: ["bos.load", "view3d.camera", "check.rule"],
  },
];

// The same model-free graph the host unit tests use: table.inline -> table.sort.
const graph = {
  formatVersion: "0.1.0",
  structure: {
    nodes: [
      { id: "rows", kind: "table.inline", version: 1 },
      { id: "sort", kind: "table.sort", version: 1 },
    ],
    edges: [{ from: "rows.table", to: "sort.table" }],
  },
  values: { rows: { rows: '[{"name":"front"}]' }, sort: { by: "name" } },
};

const fail = (msg) => { throw new Error(msg); };
const expect = (cond, msg) => cond || fail(msg);

const waitForHost = async (child, base, timeoutMs = 180000) => {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (child.exitCode !== null) fail(`host exited early with code ${child.exitCode}`);
    try {
      const res = await fetch(`${base}/api/models`);
      if (res.ok) return;
    } catch { /* not up yet */ }
    await new Promise((r) => setTimeout(r, 500));
  }
  fail(`host did not start within ${timeoutMs}ms`);
};

const run = async (host, base) => {
  const catalog = await (await fetch(`${base}/api/catalog/nodes`)).json();
  const kinds = catalog.nodes.map((n) => n.kind);
  const missing = host.expect.filter((k) => !kinds.includes(k));
  const present = host.forbid.filter((k) => kinds.includes(k));
  expect(missing.length === 0, `catalog missing ${missing.join(", ")}; got ${kinds.length} kinds`);
  expect(present.length === 0, `catalog should not carry ${present.join(", ")}`);
  console.log(`catalog: ${kinds.length} node kinds`);

  // The model roots are an empty temp folder here, so only the shape is asserted.
  const models = await (await fetch(`${base}/api/models`)).json();
  expect(Array.isArray(models), "expected a model list array");
  console.log(`models: ${models.length}`);

  const put = await fetch(`${base}/api/analyses/smoke`, {
    method: "PUT",
    headers: { "content-type": "application/json" },
    body: JSON.stringify(graph),
  });
  if (!put.ok) fail(`PUT analysis failed: ${put.status} ${await put.text()}`);
  const summary = await put.json();
  expect(/^[0-9a-f]{64}$/.test(summary.graphHash), `bad graphHash: ${summary.graphHash}`);
  console.log(`put analysis: hash ${summary.graphHash.slice(0, 12)}…`);

  const state = await (await fetch(`${base}/api/analyses/smoke/state`)).json();
  const statuses = Object.fromEntries(state.nodes.map((n) => [n.nodeId, n.status]));
  expect(statuses.rows === "Ok" && statuses.sort === "Ok",
    `expected rows/sort Ok, got ${JSON.stringify(statuses)}`);
  console.log("state: all nodes Ok");

  const slice = await (await fetch(`${base}/api/analyses/smoke/results/sort/table?take=5`)).json();
  expect(Array.isArray(slice.columns) && Array.isArray(slice.rows), "bad result slice shape");
  console.log(`result slice: ${slice.totalRows} rows total`);

  const created = await (await fetch(`${base}/api/analyses/smoke/runs`, { method: "POST" })).json();
  expect(created.graphHash === summary.graphHash, "run hash mismatch");
  const runs = await (await fetch(`${base}/api/analyses/smoke/runs`)).json();
  expect(runs.length === 1 && runs[0].fileName === created.fileName, "run not listed");
  const record = await (await fetch(`${base}/api/analyses/smoke/runs/${created.fileName}`)).text();
  expect(record.includes(summary.graphHash), "run record missing graph hash");
  console.log(`run recorded: ${created.fileName}`);

  const bos = await fetch(`${base}/api/models/nope/bos`);
  expect(bos.status === 404, `expected 404 for unknown model bytes, got ${bos.status}`);
  console.log("unknown model bytes -> 404");
};

const smoke = async (host) => {
  const port = 5300 + Math.floor(Math.random() * 2000);
  const base = `http://127.0.0.1:${port}`;
  const work = mkdtempSync(join(tmpdir(), "bof-gate-"));
  const child = spawn("dotnet", ["run", "--project", host.project, "--",
    "--port", String(port),
    "--models", join(work, "models"),
    "--cache", join(work, "cache"),
    "--store", join(work, "store")],
    { cwd: root, stdio: ["ignore", "pipe", "pipe"] });
  child.stderr.on("data", (d) => process.stderr.write(d));
  try {
    console.log(`-- ${host.name}`);
    await waitForHost(child, base);
    await run(host, base);
    return true;
  } catch (err) {
    console.error(`${host.name}: FAIL —`, err.message);
    return false;
  } finally {
    child.kill();
    try { rmSync(work, { recursive: true, force: true }); } catch { /* host may hold locks briefly */ }
  }
};

let passed = true;
for (const host of HOSTS) passed = (await smoke(host)) && passed;
if (passed) console.log("HOST SMOKE: PASS");
else {
  console.error("HOST SMOKE: FAIL");
  process.exitCode = 1;
}
