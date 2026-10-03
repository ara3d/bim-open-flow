import { defineConfig } from "vite";
import { resolve } from "path";
import { viewerAlias } from "../../viewer.config";

// The generic editor page (index.html). The toolkit's pages (studio, 3D,
// showcase, DuckDB) and the Snowdon fixture are in packages/studio-web.

// Gratify is imported from its source in deps/gratify (pattern copied from
// the former platoflow/web/vite.config.ts).
const gratify = resolve(__dirname, "../../../../deps/gratify/src/gratify");

// The dev server proxies /api to the host; override the target with
// BOF_HOST (e.g. BOF_HOST=http://127.0.0.1:5999 npm run dev).
const host = process.env.BOF_HOST ?? "http://127.0.0.1:5214";

export default defineConfig({
  // The panes still resolve viewer packages until the 3D code leaves them (viewerAlias).
  resolve: { alias: [viewerAlias, { find: "gratify", replacement: gratify }], dedupe: ["three"] },
  server: {
    // Bind the IPv4 loopback explicitly. Vite's default host is the name
    // "localhost", and Node binds only the first address dns.lookup returns,
    // which on Windows is ::1 - leaving http://127.0.0.1:5304 refused.
    host: "127.0.0.1",
    port: 5304,
    strictPort: true,
    fs: { allow: [resolve(__dirname, "../../../..")] },
    proxy: { "/api": host },
  },
});
