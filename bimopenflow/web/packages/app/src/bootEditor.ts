// Boots the editor page over the same-origin host: the vite dev server
// proxies /api to the host (vite.config.ts), and a production deployment
// serves the app from the host. index.html (main.ts) and studio.html
// (studio.ts) differ only in the chrome they pass.

import { ApiClient } from "@bimopenflow/api-client";
import { analysisParam } from "./analysisParam.js";
import { createApp, type App } from "./app.js";
import type { ChromeFactory } from "./chrome.js";
import { watchHost } from "./hostStatus.js";
import { mountHostBanner } from "./topbar.js";

export function bootEditor(chrome?: ChromeFactory): App {
  // Every call reports into one host status; the banner and the chrome show it.
  const { api, host } = watchHost((fetchFn) => new ApiClient({ baseUrl: "", fetch: fetchFn }));
  mountHostBanner(document, host);
  // `?analysis=<id>` opens that analysis, and the URL follows the open one.
  return createApp(document.getElementById("app")!, api, {
    host, chrome, initialAnalysis: analysisParam(location.search), syncUrl: true,
  });
}
