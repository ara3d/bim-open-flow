// @vitest-environment node
import { createHash } from "node:crypto";
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { createServer, get } from "node:http";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { preparedModelHandler } from "../preparedModel";

const source = Buffer.from("verified source bytes");
const prepared = Buffer.from("verified prepared bytes");
const hash = (bytes: Buffer) => createHash("sha256").update(bytes).digest("hex");
let directory: string;
let path: string;
const servers: ReturnType<typeof createServer>[] = [];

beforeEach(async () => {
  directory = await mkdtemp(join(tmpdir(), "bimflow-prepared-test-"));
  path = join(directory, "model.bfast");
  await writeFile(path, prepared);
  vi.stubGlobal("fetch", vi.fn(async () => new Response(source)));
});
afterEach(async () => {
  await Promise.all(servers.splice(0).map(server => new Promise<void>(resolve => server.close(() => resolve()))));
  await rm(directory, { recursive: true });
  vi.unstubAllGlobals();
});

async function request(id = "catalog-id") {
  const server = createServer(preparedModelHandler({ host: "http://host.test:5214", path,
    sourceHash: hash(source), preparedHash: hash(prepared) }));
  servers.push(server);
  await new Promise<void>(resolve => server.listen(0, "127.0.0.1", resolve));
  const address = server.address() as { port: number };
  return await new Promise<{ status: number; mode: string | string[] | undefined; bytes: Buffer }>((resolve, reject) => {
    get(`http://127.0.0.1:${address.port}/${id}`, response => {
      const chunks: Buffer[] = [];
      response.on("data", chunk => chunks.push(chunk));
      response.on("end", () => resolve({ status: response.statusCode!, mode: response.headers["x-bimflow-model"], bytes: Buffer.concat(chunks) }));
      response.on("error", reject);
    }).on("error", reject);
  });
}

describe("verified prepared model endpoint", () => {
  it("serves BFAST only when both actual byte hashes match", async () => {
    expect(await request()).toEqual({ status: 200, mode: "prepared-bfast", bytes: prepared });
    expect(vi.mocked(fetch).mock.calls[0][0].toString()).toBe("http://host.test:5214/api/models/catalog-id/bos");
  });
  it("returns a changed source revision unchanged instead of the old prepared model", async () => {
    const changed = Buffer.from("new source revision");
    vi.mocked(fetch).mockResolvedValue(new Response(changed));
    expect(await request()).toEqual({ status: 200, mode: "source-bos", bytes: changed });
  });
  it("falls back when the prepared file is absent", async () => {
    path = join(directory, "absent.bfast");
    expect(await request()).toEqual({ status: 200, mode: "source-bos", bytes: source });
  });
  it("falls back when prepared bytes have not been verified", async () => {
    await writeFile(path, "different prepared geometry");
    expect(await request()).toEqual({ status: 200, mode: "source-bos", bytes: source });
  });
  it("preserves upstream failure status", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response("missing", { status: 404 }));
    expect((await request()).status).toBe(404);
  });
  it.each(["catalog-id/other", "%2E%2E", "%2Fother", "%", "%5Cother"])("rejects invalid id %s before fetching from the host", async (id) => {
    expect((await request(id)).status).toBe(400);
    expect(fetch).not.toHaveBeenCalled();
  });
});
