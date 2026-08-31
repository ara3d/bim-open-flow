import { ApiClient } from "@bimopenflow/api-client";
import { createApp } from "./app.js";

// Same-origin API: the vite dev server proxies /api to the host
// (vite.config.ts); a production deployment serves the app from the host.
const api = new ApiClient({ baseUrl: "" });
createApp(document.getElementById("app")!, api);
