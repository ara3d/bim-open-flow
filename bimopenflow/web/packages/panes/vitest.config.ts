import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

const viewerSrc = (pkg: string): string =>
  fileURLToPath(
    new URL(`../../../../viewer/packages/${pkg}/src/index.ts`, import.meta.url),
  );

export default defineConfig({
  // TODO: drop the aliases once the wave's owner runs npm install and the
  // @ara3d/viewer-* file: dependencies resolve through node_modules.
  // three/jszip/hyparquet need no alias: viewer sources resolve them by
  // walking up to viewer/node_modules.
  resolve: {
    alias: [
      { find: /^@ara3d\/viewer-core$/, replacement: viewerSrc("core") },
      { find: /^@ara3d\/viewer-loaders$/, replacement: viewerSrc("loaders") },
      { find: /^@ara3d\/viewer-controls$/, replacement: viewerSrc("controls") },
    ],
  },
  test: {
    environment: "jsdom",
  },
});
