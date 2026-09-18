import { ApiClient } from "@bimopenflow/api-client";
import { createApp } from "./app.js";
import { watchHost } from "./hostStatus.js";
import { mountHostBanner } from "./topbar.js";

// Same-origin API: the vite dev server proxies /api to the host
// (vite.config.ts); a production deployment serves the app from the host.
// Every call reports into one host status; the banner and topbar show it.
const { api, host } = watchHost((fetchFn) => new ApiClient({ baseUrl: "", fetch: fetchFn }));
mountHostBanner(document, host);
createApp(document.getElementById("app")!, api, { host });
