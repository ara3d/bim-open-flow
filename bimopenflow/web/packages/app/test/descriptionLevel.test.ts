// TKT-278: one control cycles the node descriptions through hidden, brief, and
// full; the choice is kept per browser and applies to the step list and, through
// the graph package's level seam, to the canvas cards.

import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { currentDescriptionLevel, defaultDescriptionLevel, setDescriptionLevel } from "@bimopenflow/graph";
import { DESCRIPTION_PREF_KEY, loadDescriptionLevel, saveDescriptionLevel } from "../src/descriptionChoice.js";
import { createSidebar } from "../src/sidebar.js";
import { chainDocument, fakeApi, mountApp, settle, stubCanvas, tableDesc, unmountApps } from "./appHarness.js";

beforeAll(stubCanvas);
beforeEach(() => localStorage.clear());
afterEach(() => {
  unmountApps();
  setDescriptionLevel(defaultDescriptionLevel);
});

const control = (root: HTMLElement) => root.querySelector<HTMLButtonElement>(".bof-app-description-level");

describe("the stored level", () => {
  it("defaults to brief, round-trips, and ignores a foreign value", () => {
    expect(loadDescriptionLevel()).toBe("brief");
    saveDescriptionLevel("full");
    expect(loadDescriptionLevel()).toBe("full");
    localStorage.setItem(DESCRIPTION_PREF_KEY, "verbose");
    expect(loadDescriptionLevel()).toBe(defaultDescriptionLevel);
  });
});

describe("the sidebar's description control", () => {
  it("asks for the next level and shows a level without asking", () => {
    const root = document.createElement("div");
    const onDescriptionLevel = vi.fn();
    const sidebar = createSidebar(root, () => {}, { onDescriptionLevel });
    const button = control(root)!;
    expect(button.textContent).toBe("Descriptions: Brief");
    expect(button.closest(".bof-app-sidebar-panel-steps")).not.toBeNull();
    button.click();
    expect(onDescriptionLevel).toHaveBeenLastCalledWith("full");
    sidebar.setDescriptionLevel("full");
    expect(button.textContent).toBe("Descriptions: Full");
    expect(button.title).toContain("Click for hidden");
    button.click();
    expect(onDescriptionLevel).toHaveBeenLastCalledWith("hidden");
    expect(onDescriptionLevel).toHaveBeenCalledTimes(2);
  });

  it("is not drawn when the page passes no handler", () => {
    const root = document.createElement("div");
    createSidebar(root, () => {});
    expect(control(root)).toBeNull();
  });
});

describe("the level in the app", () => {
  const described = { ...tableDesc, description: "Keeps the named columns. Others are dropped. Order follows the list." };
  const open = async () => {
    const host = fakeApi({ flow: chainDocument("a", "b") }, { getNodeCatalog: async () => ({ nodes: [described] }) });
    const { root } = mountApp(host.api, { initialAnalysis: "flow" });
    await settle();
    return root;
  };
  const stepTexts = (root: HTMLElement) =>
    [...root.querySelectorAll(".bof-app-steps-description")].map((el) => el.textContent);

  it("cycles brief, full, hidden from the one control, for the steps and the cards, and remembers it", async () => {
    const root = await open();
    expect(stepTexts(root)).toEqual(["Keeps the named columns. Others are dropped.", "Keeps the named columns. Others are dropped."]);
    control(root)!.click();
    expect(currentDescriptionLevel()).toBe("full");
    expect(stepTexts(root)[0]).toBe(described.description);
    expect(control(root)!.textContent).toBe("Descriptions: Full");
    control(root)!.click();
    expect(currentDescriptionLevel()).toBe("hidden");
    expect(stepTexts(root)).toEqual([]);
    expect(localStorage.getItem(DESCRIPTION_PREF_KEY)).toBe("hidden");
  });

  it("opens at the level this browser chose last", async () => {
    saveDescriptionLevel("full");
    const root = await open();
    expect(currentDescriptionLevel()).toBe("full");
    expect(control(root)!.textContent).toBe("Descriptions: Full");
    expect(stepTexts(root)[0]).toBe(described.description);
  });
});
