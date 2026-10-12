// TKT-253: New opens a blank flow the host does not store until its first
// edit or a Save; Save on a new flow asks for a name; the picker keeps the
// user's flows apart from the samples and hides untitled flows left empty.

import { afterEach, beforeAll, beforeEach, describe, expect, it } from "vitest";
import type { FlowTemplate } from "../src/templates.js";
import { chainDocument, fakeApi, mountApp, settle, stubCanvas, unmountApps } from "./appHarness.js";

beforeAll(stubCanvas);
beforeEach(() => localStorage.clear());
afterEach(unmountApps);

const empty = { formatVersion: "0.1.0", structure: { nodes: [], edges: [] }, values: {}, layout: {} };
const templates: FlowTemplate[] = [{ id: "bfast-buffers", folder: "analyses", title: "Buffers", description: "", nodeCount: 1, kinds: [] }];

async function boot() {
  const host = fakeApi({ "bfast-buffers": chainDocument("x"), "my-doors": chainDocument("d"), "untitled-1": empty, "untitled-2": empty });
  const { app, root } = mountApp(host.api, { rememberFlow: true, templates });
  await settle();
  const puts = () => (host.calls.putAnalysis ?? []).map(([id]) => id);
  return { app, root, host, puts };
}

const pickerGroups = (root: HTMLElement) =>
  [...root.querySelectorAll<HTMLOptGroupElement>('select[aria-label="Open flow"] optgroup')]
    .map((g) => [g.label, [...g.querySelectorAll("option")].map((o) => o.value)]);

async function addNode(root: HTMLElement): Promise<void> {
  const search = root.querySelector<HTMLInputElement>(".bof-app-sidebar-panel-nodes input")!;
  search.value = "select";
  search.dispatchEvent(new Event("input"));
  root.querySelector<HTMLElement>(".bof-app-catalog .bof-app-item")!.click();
  await settle();
}

describe("New (TKT-253)", () => {
  it("opens a blank flow under a reused empty untitled id and stores nothing", async () => {
    const { app, root, puts } = await boot();
    expect(puts()).toEqual([]);
    expect(app.session().analysisId).toBeUndefined();
    expect(pickerGroups(root)).toEqual([
      ["Your flows", ["my-doors", "untitled-1"]],
      ["Samples", ["bfast-buffers"]],
    ]);
  });

  it("stores the blank flow at its first edit and connects to it", async () => {
    const { app, root, puts } = await boot();
    await addNode(root);
    expect(puts()[0]).toBe("untitled-1");
    expect(app.session().analysisId).toBe("untitled-1");
  });

  it("Save on a new flow asks for a name, stores it there, and empties the untitled id", async () => {
    const { app, root, host, puts } = await boot();
    await addNode(root);
    root.querySelector<HTMLButtonElement>(".bof-app-topbar button:nth-of-type(2)")!.click();
    await settle();
    const input = root.querySelector<HTMLInputElement>('.bof-app-name input[aria-label="Flow name"]')!;
    input.value = "My Doors";
    input.dispatchEvent(new Event("input"));
    expect(root.querySelector(".bof-app-name-note")!.textContent).toMatch(/exists/);
    input.value = "Door schedule";
    input.dispatchEvent(new Event("input"));
    root.querySelector<HTMLFormElement>(".bof-app-name form")!.requestSubmit();
    await settle();
    expect(puts()).toContain("door-schedule");
    expect(app.session().analysisId).toBe("door-schedule");
    const blanked = (host.calls.putAnalysis ?? []).filter(([id]) => id === "untitled-1").at(-1)!;
    expect(JSON.parse(blanked[1] as string).structure.nodes).toEqual([]);
    expect(pickerGroups(root)[0]).toEqual(["Your flows", ["door-schedule", "my-doors"]]);
  });
});
