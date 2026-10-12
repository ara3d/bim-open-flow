// TKT-113: the studio's Preview node picker is an explicit choice, like a
// double-click on the canvas, so the pane area shows the node picked there
// instead of staying on the flow's answer. Drives the real createApp over a
// fake host api in jsdom.

import { afterEach, beforeAll, describe, expect, it } from "vitest";
import { chainDocument, fakeApi, mountApp, settle, stubCanvas, unmountApps } from "./appHarness.js";

beforeAll(stubCanvas);
afterEach(unmountApps);

// source -> mid -> answer: "answer" is the only terminal, so it is the flow's answer.
async function openFlow(): Promise<HTMLElement> {
  const { app, root } = mountApp(fakeApi({ flow: chainDocument("source", "mid", "answer") }).api, { graphDemo: true, initialAnalysis: "flow" });
  await app.openAnalysis("flow");
  await settle();
  return root;
}

const picker = (el: HTMLElement) => el.querySelector<HTMLSelectElement>('select[aria-label="Preview node"]')!;
const header = (el: HTMLElement) => el.querySelector(".bof-app-preview-source")!.textContent ?? "";
const backButton = (el: HTMLElement) => el.querySelector<HTMLButtonElement>(".bof-app-back-to-answer")!;

function pick(el: HTMLElement, nodeId: string): void {
  const select = picker(el);
  select.value = nodeId;
  select.dispatchEvent(new Event("change"));
}

describe("Preview node picker (TKT-113)", () => {
  it("opens on the flow's answer", async () => {
    const el = await openFlow();
    expect(picker(el).value).toBe("answer");
    expect(header(el)).toMatch(/^Answer: .*\(answer\)/);
  });

  it("shows a node other than the answer once picked", async () => {
    const el = await openFlow();
    pick(el, "mid");
    await settle();
    expect(picker(el).value).toBe("mid");
    expect(header(el)).toMatch(/^Showing .*\(mid\)/);
    expect(backButton(el).hidden).toBe(false);
  });

  it("picking the answer again follows the answer", async () => {
    const el = await openFlow();
    pick(el, "source");
    await settle();
    pick(el, "answer");
    await settle();
    expect(picker(el).value).toBe("answer");
    expect(header(el)).toMatch(/^Answer: .*\(answer\)/);
    expect(backButton(el).hidden).toBe(true);
  });
});
