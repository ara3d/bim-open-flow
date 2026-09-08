import { ApiClient } from "@bimopenflow/api-client";
import { createApp } from "./app.js";

class GraphDemoApi extends ApiClient {
  override getModelBosUrl(id: string): string {
    return import.meta.env.DEV ? `/__bimflow/models/${encodeURIComponent(id)}` : super.getModelBosUrl(id);
  }
}

createApp(document.getElementById("app")!, new GraphDemoApi({ baseUrl: "" }), {
  graphDemo: true,
  initialAnalysis: "snowdon-toolkit",
});
