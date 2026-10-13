// TKT-286: opening a flow whose stored cards overlap at today's card sizes lays
// it out afresh and saves the new positions; a flow that already fits keeps its own.

import { afterEach, beforeAll, describe, expect, it } from "vitest";
import { AUTOSAVE_MS } from "../src/app.js";
import { chainDocument, fakeApi, mountApp, settle, stubCanvas, unmountApps } from "./appHarness.js";

beforeAll(stubCanvas);
afterEach(unmountApps);

const at = (layout: Record<string, { x: number; y: number }>) => ({ ...chainDocument("a", "b", "c"), layout });
const saved = async (doc: object) => {
  const host = fakeApi({ flow: doc });
  mountApp(host.api, { initialAnalysis: "flow" });
  await settle();
  await new Promise((r) => setTimeout(r, AUTOSAVE_MS + 100));
  await settle();
  const puts = host.calls.putAnalysis ?? [];
  return puts.length ? (JSON.parse(puts.at(-1)![1] as string) as { layout: Record<string, { x: number; y: number }> }).layout : undefined;
};

// Each case mounts the whole app and waits out the autosave: slow under a full parallel run.
describe("opening a flow (TKT-286)", { timeout: 20_000 }, () => {
  it("lays out a flow whose cards overlap, and saves it", async () => {
    const layout = await saved(at({ a: { x: 40, y: 40 }, b: { x: 60, y: 60 }, c: { x: 80, y: 80 } }));
    expect(layout).toBeDefined();
    expect(layout!["a"]!.x).toBeLessThan(layout!["b"]!.x);
    expect(layout!["b"]!.x).toBeLessThan(layout!["c"]!.x);
  });

  it("keeps the stored positions of a flow that fits", async () => {
    expect(await saved(at({ a: { x: 40, y: 40 }, b: { x: 400, y: 40 }, c: { x: 800, y: 40 } }))).toBeUndefined();
  });
});
