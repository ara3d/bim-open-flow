// The studio look behind the chrome seam: its command bar raises the
// controller's actions, its flow picker shows titles, and its canvas
// empty-state follows the open flow's node count.
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { ChromeActions } from "../src/chrome.js";
import { flowTitle } from "../src/studio/flowTitle.js";
import { STUDIO_THEME_PREFS, studioChrome } from "../src/studio/studioChrome.js";
import type { FlowTemplate } from "../src/templates.js";

const templates: FlowTemplate[] = [
  { id: "door-schedule", folder: "bim-analyses", title: "Door schedule", description: "", nodeCount: 3, kinds: [] },
  { id: "untitled-thing", folder: "x", title: "untitled-thing", description: "", nodeCount: 1, kinds: [] },
];

const actions = (): ChromeActions => ({
  openAnalysis: vi.fn(), newAnalysis: vi.fn(), save: vi.fn(), run: vi.fn(),
  setTheme: vi.fn(), setNodeStyle: vi.fn(), addNode: vi.fn(), selectAndFocus: vi.fn(),
  showInPane: vi.fn(), fit: vi.fn(), tidy: vi.fn(),
});

const setup = () => {
  const root = document.createElement("div");
  document.body.append(root);
  const acts = actions();
  const chrome = studioChrome({ templates })(root, acts);
  return { root, acts, chrome };
};

const byLabel = (root: HTMLElement, label: string) =>
  root.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!;

beforeEach(() => { localStorage.clear(); document.body.replaceChildren(); });

describe("flowTitle", () => {
  it("prefers a sample's title, else reads the id as words", () => {
    expect(flowTitle("door-schedule", templates)).toBe("Door schedule");
    expect(flowTitle("untitled-thing", templates)).toBe("Untitled thing");
    expect(flowTitle("clash_candidates-2", templates)).toBe("Clash candidates 2");
  });
});

describe("studioChrome", () => {
  it("raises run, save, fit, tidy, and new from its buttons", () => {
    const { root, acts } = setup();
    byLabel(root, "Run").click();
    byLabel(root, "Save").click();
    byLabel(root, "Fit").click();
    byLabel(root, "Tidy").click();
    byLabel(root, "New").click();
    expect(acts.run).toHaveBeenCalledTimes(1);
    expect(acts.save).toHaveBeenCalledTimes(1);
    expect(acts.fit).toHaveBeenCalledTimes(1);
    expect(acts.tidy).toHaveBeenCalledTimes(1);
    expect(acts.newAnalysis).toHaveBeenCalledTimes(1);
  });

  it("lists flows by title, keeps the id as the value, and opens on change", () => {
    const { root, acts, chrome } = setup();
    chrome.setAnalyses([{ id: "door-schedule", graphHash: "" }, { id: "my-flow", graphHash: "" }], "my-flow");
    const picker = root.querySelector<HTMLSelectElement>('select[aria-label="Open flow"]')!;
    expect([...picker.options].map((o) => [o.value, o.textContent])).toEqual([
      ["door-schedule", "Door schedule"], ["my-flow", "My flow"],
    ]);
    expect(picker.value).toBe("my-flow");
    expect(root.querySelector(".bof-studio-flow-id")?.textContent).toBe("my-flow");
    picker.value = "door-schedule";
    picker.dispatchEvent(new Event("change"));
    expect(acts.openAnalysis).toHaveBeenCalledWith("door-schedule");
  });

  it("shows the empty-state card only while the flow has no nodes, and counts steps", () => {
    const { root, chrome } = setup();
    const empty = root.querySelector<HTMLElement>(".bof-studio-empty")!;
    expect(empty.hidden).toBe(true);
    chrome.setNodes([]);
    expect(empty.hidden).toBe(false);
    chrome.setNodes([{ id: "a", title: "Model" }, { id: "b", title: "Filter" }]);
    expect(empty.hidden).toBe(true);
    expect(root.querySelector(".bof-studio-flow-count")?.textContent).toBe("2 steps");
  });

  it("marks dirty state, enables Save only when dirty, and shows the connection", () => {
    const { root, chrome } = setup();
    chrome.setDirty(false);
    expect(byLabel(root, "Save").disabled).toBe(true);
    chrome.setDirty(true);
    expect(byLabel(root, "Save").disabled).toBe(false);
    expect(root.classList.contains("bof-studio-is-dirty")).toBe(true);
    chrome.setConnection("offline");
    expect(root.dataset.connection).toBe("offline");
    expect(root.querySelector(".bof-studio-status-text")?.textContent).toBe("Offline");
  });

  it("offers the three canvas themes in the View menu and keeps its own theme choice", () => {
    const { root, acts, chrome } = setup();
    const theme = root.querySelector<HTMLSelectElement>('select[aria-label="Canvas theme"]')!;
    expect([...theme.options].map((o) => o.value)).toEqual(["light", "dark", "studio"]);
    chrome.setTheme("dark");
    expect(theme.value).toBe("dark");
    theme.value = "studio";
    theme.dispatchEvent(new Event("change"));
    expect(acts.setTheme).toHaveBeenCalledWith("studio");
    expect(chrome.themePrefs).toEqual(STUDIO_THEME_PREFS);
    expect(STUDIO_THEME_PREFS.fallback).toBe("studio");
  });

  it("provides every slot the controller mounts into", () => {
    const { root, chrome } = setup();
    for (const slot of [chrome.canvas, chrome.canvasHost, chrome.paneEl, chrome.askHost, chrome.stepsEl])
      expect(root.contains(slot)).toBe(true);
  });
});
