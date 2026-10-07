import { defineConfig } from "vite";
import { resolve } from "node:path";

// Builds the live graphs of the repository's GitHub Pages page into site/app
// (git-ignored): app/site.js and app/site.css, fixed names that
// site/index.html links. The page fetches data/buildings.json beside itself;
// the node catalog is bundled (src/main.ts). `npm run preview` serves all of site/
// on http://127.0.0.1:5320 after a build.
const repository = resolve(__dirname, "../../../..");
const gratify = resolve(repository, "deps/gratify/src/gratify");

export default defineConfig({
  base: "./",
  resolve: { alias: [{ find: "gratify", replacement: gratify }] },
  build: {
    outDir: resolve(repository, "site/app"),
    emptyOutDir: true,
    rollupOptions: {
      input: resolve(__dirname, "src/main.ts"),
      output: { entryFileNames: "site.js", chunkFileNames: "[name]-[hash].js", assetFileNames: "site.[ext]" },
    },
  },
  preview: { host: "127.0.0.1", port: 5320, strictPort: true },
});
