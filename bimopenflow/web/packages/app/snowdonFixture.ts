import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { resolve } from "node:path";
import type { Plugin } from "vite";
import { preparedModelHandler } from "./preparedModel";

/** A single explicit local fixture; never accepts filesystem paths from HTTP input. */
export function snowdonFixture(): Plugin {
  return { name: "bim-flow-snowdon", configureServer(server) {
    const path = process.env.BOF_SNOWDON_BFAST ?? resolve(__dirname, "../../../../artifacts/bim-flow/snowdon.bfast");
    server.middlewares.use("/__bimflow/models", preparedModelHandler({
      host: process.env.BOF_HOST ?? "http://127.0.0.1:5214",
      path,
      // Exact source/prepared pair verified in docs/bim-flow-3d.md.
      sourceHash: "fc31c4463d9eb958ae8de3d853cfc8224b9469477b419857fbc929956c9cc51d",
      preparedHash: "313c247e01a9aeee10d373b8d8ddfb9c13fc5ebb709945e75defcd763f33465b",
    }));
    server.middlewares.use("/__bimflow/snowdon.bfast", async (request, response) => {
      if (request.method !== "GET" && request.method !== "HEAD") { response.statusCode = 405; response.end(); return; }
      try {
        const info = await stat(path);
        response.setHeader("Content-Type", "application/octet-stream");
        response.setHeader("Content-Length", info.size);
        response.setHeader("Cache-Control", "no-store");
        if (request.method === "HEAD") { response.end(); return; }
        const stream = createReadStream(path);
        response.on("close", () => stream.destroy());
        stream.on("error", () => response.destroy());
        stream.pipe(response);
      } catch {
        response.statusCode = 404;
        response.end("Snowdon is unavailable. Set BOF_SNOWDON_BFAST to your prepared Snowdon BFAST with BIM tables.");
      }
    });
  } };
}
