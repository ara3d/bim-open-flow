// TKT-255: the editor flooded PUT /api/session, about 150 in a second while
// the page loaded under a pane that toggles visibility, and one a second
// while idle. Drives the real createApp: mounting, visibility changes, and
// a selection change, counting the PUTs the host receives.

import { afterEach, beforeAll, describe, expect, it } from "vitest";
import type { EditorSession } from "@bimopenflow/contracts";
import { SESSION_DEBOUNCE_MS } from "../src/editorSession.js";
import { chainDocument, fakeApi, mountApp, settle, stubCanvas, unmountApps } from "./appHarness.js";

beforeAll(() => {
  stubCanvas();
  Object.defineProperty(document, "visibilityState", { configurable: true, get: () => "visible" });
});
afterEach(unmountApps);

const quiet = async () => {
  await new Promise((r) => setTimeout(r, SESSION_DEBOUNCE_MS * 2));
  await settle();
};

async function mount() {
  // The host keeps the last session put, as /api/session does.
  let held: EditorSession = { selection: [] };
  const host = fakeApi({ flow: chainDocument("source", "answer") }, {
    putSession: async (body: EditorSession) => (held = body),
    getSession: async () => held,
  });
  const { root } = mountApp(host.api, { initialAnalysis: "flow" });
  await settle();
  await quiet();
  const puts = () => (host.calls.putSession ?? []).length;
  const otherTabTakesOver = () => { held = { analysisId: "elsewhere", selection: [] }; };
  return { root, puts, otherTabTakesOver };
}

const toggleVisibility = (times: number) => {
  for (let i = 0; i < times; i++) document.dispatchEvent(new Event("visibilitychange"));
};

describe("session reports (TKT-255)", () => {
  it("mounting reports the open flow once, and an idle tab reports nothing more", async () => {
    const { puts } = await mount();
    expect(puts()).toBe(1);
    await quiet();
    expect(puts()).toBe(1);
  });

  it("a burst of visibility changes sends nothing while the host already holds this tab's session", async () => {
    const { puts } = await mount();
    toggleVisibility(150);
    await quiet();
    expect(puts()).toBe(1);
  });

  it("a burst of visibility changes after another tab took over sends exactly one", async () => {
    const { puts, otherTabTakesOver } = await mount();
    otherTabTakesOver();
    toggleVisibility(150);
    await quiet();
    expect(puts()).toBe(2);
  });

  it("a selection change sends one after the debounce", async () => {
    const { root, puts } = await mount();
    root.querySelector<HTMLElement>('.bof-app-steps-step[data-node-id="answer"]')!.click();
    expect(puts()).toBe(1);
    await quiet();
    expect(puts()).toBe(2);
  });
});
