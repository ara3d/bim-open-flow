import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";
import { toolkitAlias } from "../../toolkit.config";

// The same gratify source alias as packages/app, so every module under test
// resolves gratify from the submodule.
const gratify = fileURLToPath(
  new URL("../../../../deps/gratify/src/gratify", import.meta.url),
);

export default defineConfig({
  resolve: { alias: [toolkitAlias, { find: "gratify", replacement: gratify }], dedupe: ["three"] },
  test: {
    environment: "jsdom",
  },
});
