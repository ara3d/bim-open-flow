import { beforeEach, describe, expect, it } from "vitest";
import { ABSENT_NOTE, createStartPage } from "../src/startPage.js";
import type { FlowTemplate } from "../src/templates.js";

const t = (id: string, folder: string): FlowTemplate => ({
  id, folder, title: `Title ${id}`, description: `about ${id}`, nodeCount: 3, kinds: ["duck.read", "table.sort"],
});

const TEMPLATES = [t("a1", "analyses"), t("a2", "analyses"), t("b1", "bim-analyses")];

const setup = () => {
  document.body.replaceChildren();
  const host = document.createElement("div");
  document.body.appendChild(host);
  const calls: string[] = [];
  const page = createStartPage(host, {
    templates: TEMPLATES,
    onOpen: (id) => calls.push(`open:${id}`),
    onCopy: (id) => calls.push(`copy:${id}`),
    onBlank: () => calls.push("blank"),
  });
  const card = (id: string) => host.querySelector(`.bof-app-start-card[data-id="${id}"]`) as HTMLElement;
  return { host, calls, page, card };
};

describe("createStartPage", () => {
  let s: ReturnType<typeof setup>;
  beforeEach(() => {
    s = setup();
    s.page.setPresent(["a1", "b1"]);
    s.page.show();
  });

  it("renders one section per group with a card per template", () => {
    const labels = [...s.host.querySelectorAll(".bof-app-start-group-label")].map((h) => h.textContent);
    expect(labels).toEqual(["Tables", "BIM"]);
    const sections = [...s.host.querySelectorAll(".bof-app-start-group")];
    expect(sections.map((g) => g.querySelectorAll(".bof-app-start-card").length)).toEqual([2, 1]);
    const a1 = s.card("a1");
    expect(a1.textContent).toContain("Title a1");
    expect(a1.textContent).toContain("about a1");
    expect(a1.textContent).toContain("3 nodes");
    expect([...a1.querySelectorAll(".bof-app-start-kind")].map((k) => k.textContent)).toEqual(["duck.read", "table.sort"]);
  });

  it("opens a present card on click", () => {
    s.card("a1").click();
    expect(s.calls).toEqual(["open:a1"]);
  });

  it("dims an absent card, names why, and ignores clicks", () => {
    const a2 = s.card("a2");
    expect(a2.classList.contains("bof-app-start-card-absent")).toBe(true);
    expect(a2.textContent).toContain(ABSENT_NOTE);
    expect(a2.querySelector(".bof-app-start-copy")).toBeNull();
    a2.click();
    expect(s.calls).toEqual([]);
  });

  it("Copy calls onCopy without opening", () => {
    (s.card("b1").querySelector(".bof-app-start-copy") as HTMLElement).click();
    expect(s.calls).toEqual(["copy:b1"]);
  });

  it("the Blank flow card calls onBlank", () => {
    (s.host.querySelector(".bof-app-start-blank") as HTMLElement).click();
    expect(s.calls).toEqual(["blank"]);
  });

  it("Escape and the close button hide it", () => {
    expect(s.page.isOpen()).toBe(true);
    document.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" }));
    expect(s.page.isOpen()).toBe(false);
    s.page.show();
    (s.host.querySelector(".bof-app-start-close") as HTMLElement).click();
    expect(s.page.isOpen()).toBe(false);
  });

  it("setPresent re-renders presence", () => {
    s.page.setPresent(["a2"]);
    expect(s.card("a1").classList.contains("bof-app-start-card-absent")).toBe(true);
    expect(s.card("a2").classList.contains("bof-app-start-card-absent")).toBe(false);
    s.card("a2").click();
    expect(s.calls).toEqual(["open:a2"]);
  });

  it("injects its style once and dispose removes the overlay", () => {
    setup();
    expect(document.querySelectorAll("#bof-app-start-styles").length).toBe(1);
    s.page.dispose();
    expect(s.host.querySelector(".bof-app-start")).toBeNull();
  });
});
