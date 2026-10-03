import { defineConfig } from "vitest/config";
import { viewerAlias } from "../../viewer.config";

// The panes import the viewer packages from source; same alias as packages/panes.
export default defineConfig({
  resolve: { alias: [viewerAlias], dedupe: ["three"] },
  test: {
    environment: "jsdom",
  },
});
