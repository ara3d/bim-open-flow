import { defineConfig } from "vite";
import { resolve } from "path";

// Gratify is imported from the submodule source (pattern copied from
// platoflow/web/vite.config.ts).
const gratify = resolve(__dirname, "../../../../submodules/gratify/src/gratify");

// The dev server proxies /api to the host; override the target with
// BOF_HOST (e.g. BOF_HOST=http://127.0.0.1:5999 npm run dev).
const host = process.env.BOF_HOST ?? "http://127.0.0.1:5214";

export default defineConfig({
  resolve: { alias: { gratify } },
  server: {
    port: 5300,
    strictPort: true,
    fs: { allow: [resolve(__dirname, "../../../..")] },
    proxy: { "/api": host },
  },
});
