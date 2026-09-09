import { defineConfig } from "vite";
import { resolve } from "path";
import { toolkitAlias } from "../../toolkit.config";
import { snowdonFixture } from "./snowdonFixture";

// Gratify is imported from the submodule source (pattern copied from
// platoflow/web/vite.config.ts).
const gratify = resolve(__dirname, "../../../../submodules/gratify/src/gratify");

// The dev server proxies /api to the host; override the target with
// BOF_HOST (e.g. BOF_HOST=http://127.0.0.1:5999 npm run dev).
const host = process.env.BOF_HOST ?? "http://127.0.0.1:5214";

export default defineConfig({
  plugins: [snowdonFixture()],
  build: { rollupOptions: { input: { app: resolve(__dirname, "index.html"), graphDemo: resolve(__dirname, "3d.html"), showcase: resolve(__dirname, "showcase.html") } } },
  resolve: { alias: [toolkitAlias, { find: "gratify", replacement: gratify }], dedupe: ["three"] },
  server: {
    // Bind the IPv4 loopback explicitly. Vite's default host is the name
    // "localhost", and Node binds only the first address dns.lookup returns,
    // which on Windows is ::1 - leaving http://127.0.0.1:5300 refused.
    host: "127.0.0.1",
    port: 5300,
    strictPort: true,
    fs: { allow: [resolve(__dirname, "../../../..")] },
    proxy: { "/api": host },
  },
});
