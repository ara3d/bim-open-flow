import { createHash } from "node:crypto";
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import type { IncomingMessage, ServerResponse } from "node:http";

export interface PreparedModelOptions {
  host: string;
  path: string;
  sourceHash: string;
  preparedHash: string;
}

/** Serves a prepared model only for a byte-verified source/preparation pair. */
export function preparedModelHandler(options: PreparedModelOptions) {
  let verified: { key: string; result: Promise<boolean> } | undefined;
  const verify = async () => {
    const info = await stat(options.path);
    const key = `${info.size}:${info.mtimeMs}:${info.ctimeMs}`;
    if (verified?.key !== key) {
      const result = (async () => {
        const hash = createHash("sha256");
        for await (const chunk of createReadStream(options.path)) hash.update(chunk);
        return hash.digest("hex") === options.preparedHash;
      })();
      verified = { key, result };
    }
    return await verified.result ? info : null;
  };
  return async (request: IncomingMessage, response: ServerResponse) => {
    if (request.method !== "GET" && request.method !== "HEAD") {
      response.statusCode = 405;
      response.end();
      return;
    }
    const encodedId = request.url?.split("?", 1)[0]?.slice(1);
    let id: string | undefined;
    try { id = encodedId && decodeURIComponent(encodedId); } catch { /* Invalid URL encoding. */ }
    if (!id || id === "." || id === ".." || id.includes("/") || id.includes("\\")) {
      response.statusCode = 400;
      response.end("A model catalog id is required.");
      return;
    }
    const abort = new AbortController();
    const timeout = setTimeout(() => abort.abort(), 30_000);
    response.on("close", () => abort.abort());
    try {
      // Recheck source bytes each time: a catalog id may now name a new revision.
      const sourceUrl = new URL(`/api/models/${encodeURIComponent(id)}/bos`, options.host);
      const [source, prepared] = await Promise.all([
        fetch(sourceUrl, { signal: abort.signal }),
        verify().catch(() => null),
      ]);
      response.setHeader("Cache-Control", "no-store");
      if (!source.ok) {
        await source.body?.cancel();
        response.statusCode = source.status;
        response.end("The model host could not supply this model.");
        return;
      }
      const bytes = new Uint8Array(await source.arrayBuffer());
      if (abort.signal.aborted) return;
      const sourceHash = createHash("sha256").update(bytes).digest("hex");
      const usePrepared = prepared && sourceHash === options.sourceHash;
      response.setHeader("Content-Type", "application/octet-stream");
      response.setHeader("Content-Length", usePrepared ? prepared.size : bytes.byteLength);
      response.setHeader("X-BimFlow-Model", usePrepared ? "prepared-bfast" : "source-bos");
      if (request.method === "HEAD") { response.end(); return; }
      if (!usePrepared) { response.end(bytes); return; }
      const stream = createReadStream(options.path);
      response.on("close", () => stream.destroy());
      stream.on("error", () => response.destroy());
      stream.pipe(response);
    } catch {
      if (!response.destroyed) {
        response.statusCode = 502;
        response.end("The model could not be loaded from the host.");
      }
    } finally {
      clearTimeout(timeout);
    }
  };
}
