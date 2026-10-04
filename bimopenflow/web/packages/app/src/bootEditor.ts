// Boots the editor page over the same-origin host: the vite dev server
// proxies /api to the host (vite.config.ts), and a production deployment
// serves the app from the host. Pages differ in the chrome, the panes, and
// the templates they pass; the toolkit's pages live in @bimopenflow/studio-web.

import { ApiClient } from "@bimopenflow/api-client";
import { analysisParam } from "./analysisParam.js";
import { createApp, type App, type AppOptions } from "./app.js";
import { mountHostBanner, watchHost } from "@bimopenflow/client/host";

export type BootOptions = Pick<AppOptions, "chrome" | "panes" | "templates">;

export function bootEditor(options: BootOptions = {}): App {
  // Every call reports into one host status; the banner and the chrome show it.
  const { api, host } = watchHost((fetchFn) => new ApiClient({ baseUrl: "", fetch: fetchFn }));
  mountHostBanner(document, host);
  // `?analysis=<id>` opens that analysis, and the URL follows the open one.
  return createApp(document.getElementById("app")!, api, {
    ...options, host, initialAnalysis: analysisParam(location.search), syncUrl: true,
  });
}
