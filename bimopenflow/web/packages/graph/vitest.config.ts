import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// The same gratify source alias as packages/app, so every module under test
// resolves gratify from the submodule.
const gratify = fileURLToPath(
  new URL("../../../../deps/gratify/src/gratify", import.meta.url),
);

export default defineConfig({
  resolve: { alias: [{ find: "gratify", replacement: gratify }], dedupe: ["three"] },
  test: {
    environment: "jsdom",
    // The first headless render of every sample graph takes ~4.4 s warm and
    // more under load (TKT-141); the 5 s default made it a flake.
    testTimeout: 30_000,
  },
});
