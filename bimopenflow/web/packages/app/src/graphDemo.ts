// Entry for 3d.html: the graph demo shell over the BIM-profile host. The
// analysis to open comes from the `analysis` query parameter
// (3d.html?analysis=color-by-category) and defaults to the Snowdon toolkit.

import { ApiClient } from "@bimopenflow/api-client";
import { analysisFromSearch } from "./analysisParam.js";
import { createApp } from "./app.js";
import { mountHostBanner, watchHost } from "@bimopenflow/client/host";

class GraphDemoApi extends ApiClient {
  override getModelBosUrl(id: string): string {
    return import.meta.env.DEV ? `/__bimflow/models/${encodeURIComponent(id)}` : super.getModelBosUrl(id);
  }
}

// Boot only inside 3d.html; importing this module elsewhere (tests) is side-effect free.
const root = document.getElementById("app");
if (root) {
  const { api, host } = watchHost((fetchFn) => new GraphDemoApi({ baseUrl: "", fetch: fetchFn }));
  mountHostBanner(document, host);
  createApp(root, api, { graphDemo: true, initialAnalysis: analysisFromSearch(location.search), host });
}
