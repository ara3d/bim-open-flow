import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";
import { viewerAlias } from "../../viewer.config";

// Same gratify source alias as vite.config.ts, so any module under test that
// imports gratify resolves it from deps/gratify.
const gratify = fileURLToPath(
  new URL("../../../../deps/gratify/src/gratify", import.meta.url),
);

export default defineConfig({
  resolve: { alias: [viewerAlias, { find: "gratify", replacement: gratify }], dedupe: ["three"] },
  test: {
    environment: "jsdom",
    // duckdbAskLog.test.ts imports the whole DuckDB page in beforeAll; a cold transform under
    // load took longer than the 10 s default and failed the web smoke once (2026-10-03).
    hookTimeout: 30_000,
  },
});
