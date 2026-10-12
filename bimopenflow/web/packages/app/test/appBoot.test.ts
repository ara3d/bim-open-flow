// TKT-254: what the editor opens at the root URL. The flow the URL names,
// else the one last opened on this origin, else the Start panel over a blank
// flow; never a flow and the panel at once, never the first id by name.

import { afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";
import { LAST_FLOW_KEY } from "../src/lastFlow.js";
import { chainDocument, fakeApi, mountApp, settle, stubCanvas, unmountApps } from "./appHarness.js";

beforeAll(stubCanvas);
beforeEach(() => localStorage.clear());
afterEach(unmountApps);

const store = () => fakeApi({ "a-red-first": chainDocument("x"), "door-schedule": chainDocument("doors", "table") });
const startOpen = (root: HTMLElement) => !root.querySelector<HTMLElement>(".bof-app-start")!.hidden;

async function boot(options: Parameters<typeof mountApp>[1]) {
  const host = store();
  const mounted = mountApp(host.api, { rememberFlow: true, ...options });
  await settle();
  const opened = (host.calls.getAnalysis ?? []).map(([id]) => id);
  return { ...mounted, opened };
}

describe("the root URL (TKT-254)", () => {
  it("shows the Start panel over a blank flow on a first visit, and opens no stored flow", async () => {
    const { app, root, opened } = await boot({});
    expect(startOpen(root)).toBe(true);
    expect(opened).not.toContain("a-red-first");
    expect(opened).not.toContain("door-schedule");
    // The blank flow is not stored yet, so the session names no flow (TKT-253).
    expect(app.session().analysisId).toBeUndefined();
  });

  it("reopens the flow last opened here, without the Start panel", async () => {
    localStorage.setItem(LAST_FLOW_KEY, "door-schedule");
    const { app, root } = await boot({});
    expect(app.session().analysisId).toBe("door-schedule");
    expect(startOpen(root)).toBe(false);
  });

  it("falls back to the Start panel when the remembered flow is gone", async () => {
    localStorage.setItem(LAST_FLOW_KEY, "deleted-since");
    const { root, opened } = await boot({});
    expect(startOpen(root)).toBe(true);
    expect(opened).not.toContain("a-red-first");
  });

  it("opens the flow the URL names, without the Start panel, and remembers it", async () => {
    const { app, root } = await boot({ initialAnalysis: "a-red-first" });
    expect(app.session().analysisId).toBe("a-red-first");
    expect(startOpen(root)).toBe(false);
    expect(localStorage.getItem(LAST_FLOW_KEY)).toBe("a-red-first");
  });

  it("hides the Start panel when a flow is opened some other way", async () => {
    const { app, root } = await boot({});
    expect(startOpen(root)).toBe(true);
    await app.openAnalysis("door-schedule");
    expect(startOpen(root)).toBe(false);
  });
});
