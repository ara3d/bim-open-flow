import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

// Same gratify source alias as vite.config.ts, so any module under test that
// imports gratify resolves it from the submodule.
const gratify = fileURLToPath(
  new URL("../../../../submodules/gratify/src/gratify", import.meta.url),
);

export default defineConfig({
  resolve: { alias: { gratify } },
  test: {
    environment: "jsdom",
  },
});
