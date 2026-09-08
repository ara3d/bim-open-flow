import { ApiClient } from "@bimopenflow/api-client";
import { createApp } from "./app.js";

createApp(document.getElementById("app")!, new ApiClient({ baseUrl: "" }), {
  graphDemo: true,
  initialAnalysis: "snowdon-toolkit",
});
