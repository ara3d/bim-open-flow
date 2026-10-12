import { beforeEach, describe, expect, it } from "vitest";
import { ABSENT_HEADING, ABSENT_NOTE, createStartPage } from "../src/startPage.js";
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

  it("renders the seeded groups first, a card per template", () => {
    const seeded = [...s.host.querySelectorAll(".bof-app-start > .bof-app-start-group")];
    expect(seeded.map((g) => g.querySelector(".bof-app-start-group-label")!.textContent)).toEqual(["Tables", "BIM"]);
    expect(seeded.map((g) => [...g.querySelectorAll<HTMLElement>(".bof-app-start-card")].map((c) => c.dataset.id))).toEqual([["a1"], ["b1"]]);
    const a1 = s.card("a1");
    expect(a1.textContent).toContain("Title a1");
    expect(a1.textContent).toContain("about a1");
    expect(a1.textContent).toContain("3 nodes");
    expect([...a1.querySelectorAll(".bof-app-start-kind")].map((k) => k.textContent)).toEqual(["duck.read", "table.sort"]);
  });

  it("collapses the templates the host did not seed under one heading after the seeded ones (TKT-254)", () => {
    const absent = s.host.querySelector<HTMLDetailsElement>(".bof-app-start > .bof-app-start-absent")!;
    expect(absent.open).toBe(false);
    expect(absent.querySelector("summary")!.textContent).toBe(`${ABSENT_HEADING} (1)`);
    expect([...absent.querySelectorAll<HTMLElement>(".bof-app-start-card")].map((c) => c.dataset.id)).toEqual(["a2"]);
    expect(s.host.querySelector(".bof-app-start")!.lastElementChild).toBe(absent);
  });

  it("keeps the collapsed list open across a re-render", () => {
    s.host.querySelector<HTMLDetailsElement>(".bof-app-start-absent")!.open = true;
    s.page.setPresent(["a1"]);
    expect(s.host.querySelector<HTMLDetailsElement>(".bof-app-start-absent")!.open).toBe(true);
  });

  it("has no collapsed heading when every template is seeded", () => {
    s.page.setPresent(["a1", "a2", "b1"]);
    expect(s.host.querySelector(".bof-app-start-absent")).toBeNull();
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
