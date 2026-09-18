// Entry for 3d.html: the graph demo shell over the BIM-profile host. The
// analysis to open comes from the `analysis` query parameter
// (3d.html?analysis=color-by-category) and defaults to the Snowdon toolkit.

import { ApiClient } from "@bimopenflow/api-client";
import { createApp } from "./app.js";

export const DEFAULT_ANALYSIS = "snowdon-toolkit";

/** The `analysis` query value when present and non-blank, else the default. */
export const analysisFromSearch = (search: string, fallback = DEFAULT_ANALYSIS): string =>
  new URLSearchParams(search).get("analysis")?.trim() || fallback;

class GraphDemoApi extends ApiClient {
  override getModelBosUrl(id: string): string {
    return import.meta.env.DEV ? `/__bimflow/models/${encodeURIComponent(id)}` : super.getModelBosUrl(id);
  }
}

// Boot only inside 3d.html; importing this module elsewhere (tests) is side-effect free.
const root = document.getElementById("app");
if (root) createApp(root, new GraphDemoApi({ baseUrl: "" }), { graphDemo: true, initialAnalysis: analysisFromSearch(location.search) });
