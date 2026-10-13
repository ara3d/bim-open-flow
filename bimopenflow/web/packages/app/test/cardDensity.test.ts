// TKT-296: one control toggles the canvas cards between full and compact; the
// choice is kept per browser and reaches the canvas through the graph
// package's density seam.

import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { defaultCardDensity, setCardDensity } from "@bimopenflow/graph";
import { CARD_DENSITY_PREF_KEY, loadCardDensity, saveCardDensity } from "../src/cardDensityChoice.js";
import { createSidebar } from "../src/sidebar.js";

beforeEach(() => localStorage.clear());
afterEach(() => setCardDensity(defaultCardDensity));

const control = (root: HTMLElement) => root.querySelector<HTMLButtonElement>(".bof-app-card-density");

describe("the stored density", () => {
  it("defaults to full, round-trips, and ignores a foreign value", () => {
    expect(loadCardDensity()).toBe("full");
    saveCardDensity("compact");
    expect(loadCardDensity()).toBe("compact");
    localStorage.setItem(CARD_DENSITY_PREF_KEY, "tiny");
    expect(loadCardDensity()).toBe(defaultCardDensity);
  });
});

describe("the sidebar's card density control", () => {
  it("asks for the other density and shows one without asking", () => {
    const root = document.createElement("div");
    const onCardDensity = vi.fn();
    const sidebar = createSidebar(root, () => {}, { onCardDensity });
    const button = control(root)!;
    expect(button.textContent).toBe("Cards: Full");
    expect(button.closest(".bof-app-sidebar-panel-steps")).not.toBeNull();
    button.click();
    expect(onCardDensity).toHaveBeenLastCalledWith("compact");
    sidebar.setCardDensity("compact");
    expect(button.textContent).toBe("Cards: Compact");
    button.click();
    expect(onCardDensity).toHaveBeenLastCalledWith("full");
  });

  it("is not drawn when the page passes no handler", () => {
    const root = document.createElement("div");
    createSidebar(root, () => {}, { onDescriptionLevel: () => {} });
    expect(control(root)).toBeNull();
  });
});
