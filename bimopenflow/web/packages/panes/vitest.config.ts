import { defineConfig } from "vitest/config";
import { toolkitAlias } from "../../toolkit.config";

export default defineConfig({
  resolve: { alias: [toolkitAlias], dedupe: ["three"] },
  test: {
    environment: "jsdom",
  },
});
