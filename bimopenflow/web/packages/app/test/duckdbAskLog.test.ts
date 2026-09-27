// TKT-25: the Ask log must keep a fixed height and scroll internally, so the
// editor above it never resizes while a reply streams in.
//
// jsdom does not run a real layout engine, so a rendered element's
// getBoundingClientRect is always zero regardless of CSS (verified by hand
// while writing this test): a browser-accurate "did the editor move" check
// needs a real browser. Two checks stand in, together covering the actual
// fix:
//   1. The stylesheet text itself: the visible log rule sets a fixed
//      `height`, not `max-height` (the bug: a max-height rule's box grows
//      with content, one Ask event at a time, and steals space from the
//      flex-basis-1 editor beside it).
//   2. The real `appendAskLine` export, run for twenty events against the
//      log element the module creates, to check the transcript's scrolling
//      contract: the newest line is always brought into view.
import { afterEach, beforeAll, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const css = readFileSync(join(here, "..", "src", "duckdbDemo.css"), "utf8");

describe("duckdbDemo.css: the Ask log's box", () => {
  it("is a fixed height, not a max-height that grows with content", () => {
    const rule = css.match(/#duck-ask-log:not\(\[hidden\]\)\s*\{[^}]*\}/)?.[0];
    expect(rule, "no rule for the visible log").toBeTruthy();
    expect(rule).toMatch(/(?<!max-)\bheight:\s*180px/);
    expect(rule).not.toMatch(/max-height/);
  });

  it("scrolls its own overflow instead of expanding", () => {
    const rule = css.match(/#duck-ask-log:not\(\[hidden\]\)\s*\{[^}]*\}/)?.[0]!;
    expect(rule).toMatch(/overflow(-y)?:\s*auto/);
  });

  it("leaves the editor's flex-basis untouched by the log", () => {
    const rule = css.match(/#duck-editor\.bof-app-root\s*\{[^}]*\}/)?.[0]!;
    // flex: 1 1 0 with its own min-height: 0 — sized only by the flex
    // container, never by how tall a sibling's content has grown.
    expect(rule).toMatch(/flex:\s*1\s+1\s+0\b/);
    expect(rule).toMatch(/min-height:\s*0\b/);
  });
});

describe("appendAskLine: the transcript keeps the newest line visible", () => {
  let appendAskLine: (kind: string, text: string) => HTMLElement;
  let log: HTMLElement;
  let editor: HTMLElement;

  beforeAll(async () => {
    // duckdbDemo.ts is the page's entry module: importing it runs the whole
    // bootstrap (it builds #duck-ask-log and #duck-editor, starts the host
    // watcher, and tries to load the demo). Give it a container and a
    // fetch that fails fast, so the bootstrap runs to completion without a
    // real host, leaving the real elements and the real appendAskLine
    // export behind for this test to drive directly.
    document.body.innerHTML = '<div id="duckdb-demo"></div>';
    vi.stubGlobal("fetch", vi.fn(() => Promise.reject(new Error("no host in this test"))));
    const style = document.createElement("style");
    style.textContent = css;
    document.head.append(style);
    const mod = await import("../src/duckdbDemo.js");
    appendAskLine = mod.appendAskLine;
    log = document.querySelector<HTMLElement>("#duck-ask-log")!;
    editor = document.querySelector<HTMLElement>("#duck-editor")!;
    editor.classList.add("bof-app-root"); // normally added once the app opens; add it to exercise the real rule
    log.hidden = false; // normally cleared by the first Ask
  });

  afterEach(() => {
    // Let the module's own cleanup run so its host-status probe timer
    // clears rather than firing after the test file has moved on.
    window.dispatchEvent(new Event("pagehide"));
  });

  it("keeps the log's computed height fixed across twenty streamed events", () => {
    const before = getComputedStyle(log).height;
    let fakeScrollHeight = 0;
    Object.defineProperty(log, "scrollHeight", { configurable: true, get: () => fakeScrollHeight });
    for (let i = 0; i < 20; i++) {
      fakeScrollHeight += 18; // a line's worth of height, once more content is appended
      appendAskLine("agent", `Agent: reply number ${i}`);
      // The newest line is what appendAskLine just scrolled to.
      expect(log.scrollTop).toBe(fakeScrollHeight);
    }
    expect(log.children).toHaveLength(20);
    expect(getComputedStyle(log).height).toBe(before);
    expect(getComputedStyle(log).height).toBe("180px");
  });

  it("never changes the editor's flex sizing while the log fills up", () => {
    const flexBefore = getComputedStyle(editor).flex;
    const minHeightBefore = getComputedStyle(editor).minHeight;
    for (let i = 0; i < 20; i++) appendAskLine("tool", `-> tool.call(step=${i})`);
    expect(getComputedStyle(editor).flex).toBe(flexBefore);
    expect(getComputedStyle(editor).minHeight).toBe(minHeightBefore);
  });
});
