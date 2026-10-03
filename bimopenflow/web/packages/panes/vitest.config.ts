import { defineConfig } from "vitest/config";
import { viewerAlias } from "../../viewer.config";

export default defineConfig({
  resolve: { alias: [viewerAlias], dedupe: ["three"] },
  test: {
    environment: "jsdom",
  },
});
