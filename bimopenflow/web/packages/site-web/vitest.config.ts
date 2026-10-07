import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

const gratify = fileURLToPath(new URL("../../../../deps/gratify/src/gratify", import.meta.url));

export default defineConfig({
  resolve: { alias: [{ find: "gratify", replacement: gratify }] },
  test: { environment: "node" },
});
