import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";
import { toolkitAlias } from "../../toolkit.config";

// Same gratify source alias as vite.config.ts, so any module under test that
// imports gratify resolves it from the submodule.
const gratify = fileURLToPath(
  new URL("../../../../submodules/gratify/src/gratify", import.meta.url),
);

export default defineConfig({
  resolve: { alias: [toolkitAlias, { find: "gratify", replacement: gratify }], dedupe: ["three"] },
  test: {
    environment: "jsdom",
  },
});
