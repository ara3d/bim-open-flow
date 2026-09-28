import { describe, expect, it, vi } from "vitest";
import type { Problem } from "../src/graphProblems.js";
import { createProblemsPanel } from "../src/problemsPanel.js";

const problems: Problem[] = [
  { nodeId: "a", title: "Load", status: "Error", text: "boom", depth: 0 },
  { nodeId: "r", title: "Report", status: "EffectPending", text: "Run to see results", depth: 1 },
];

function mount() {
  const host = document.createElement("div");
  document.body.appendChild(host);
  const onSelect = vi.fn();
  const panel = createProblemsPanel(host, { onSelect });
  const strip = host.querySelector<HTMLElement>(".bof-app-problems-strip")!;
  const list = host.querySelector<HTMLElement>(".bof-app-problems-list")!;
  return { host, panel, onSelect, strip, list };
}

describe("createProblemsPanel", () => {
  it("is hidden when there are no problems", () => {
    const { panel, strip } = mount();
    panel.render([]);
    expect(strip.style.display).toBe("none");
  });

  it("shows the summary and one entry per problem", () => {
    const { panel, strip, host } = mount();
    panel.render(problems);
    expect(strip.style.display).not.toBe("none");
    expect(host.querySelector(".bof-app-problems-summary")!.textContent).toBe("2 problems · 1 error");
    expect(host.querySelectorAll(".bof-app-problems-entry")).toHaveLength(2);
    expect(host.querySelector('[data-node-id="r"]')!.classList.contains("bof-app-problems-info")).toBe(true);
    expect(host.querySelector('[data-node-id="a"]')!.classList.contains("bof-app-problems-info")).toBe(false);
  });

  it("toggles the list when the summary is clicked", () => {
    const { panel, host, list } = mount();
    panel.render(problems);
    expect(list.style.display).toBe("none");
    host.querySelector<HTMLElement>(".bof-app-problems-summary")!.click();
    expect(list.style.display).not.toBe("none");
    host.querySelector<HTMLElement>(".bof-app-problems-summary")!.click();
    expect(list.style.display).toBe("none");
  });

  it("calls onSelect with the node id of a clicked entry", () => {
    const { panel, host, onSelect } = mount();
    panel.render(problems);
    host.querySelector<HTMLElement>('[data-node-id="a"]')!.click();
    expect(onSelect).toHaveBeenCalledWith("a");
  });

  it("removes the strip on dispose", () => {
    const { panel, host } = mount();
    panel.dispose();
    expect(host.querySelector(".bof-app-problems-strip")).toBeNull();
  });
});
