import { describe, expect, it, vi } from "vitest";
import type { FolderListing, PathStatus, SuggestionList } from "@bimopenflow/contracts";
import { FilePathControls, isMissing, shownPath, startFolder, type FileProvider } from "../src/filePathControl.js";
import { slotControl } from "../src/canvasSlots.js";

const tick = () => new Promise((resolve) => setTimeout(resolve, 0));
const ROOT = "C:/models";
const status = (path: string, exists: boolean, relative?: string): PathStatus =>
  ({ path, exists, ...(relative ? { root: ROOT, relative } : {}) });
const suggestions = async (): Promise<SuggestionList> => ({ status: "Ok", values: [{ value: `${ROOT}/a.bos`, detail: "a.bos" }] });

function provider(stat: (p: string) => PathStatus, listings: Record<string, FolderListing> = {}): FileProvider {
  return {
    stat: async (p) => stat(p),
    browse: async (dir) => listings[dir] ?? { dir, roots: [ROOT], entries: [] },
  };
}

describe("slotControl", () => {
  it("gives a FilePath that suggests files the file picker, and leaves other FilePaths a field", () => {
    const suggest = { kind: "FilesUnderRoots" as const, source: ".bos" };
    expect(slotControl({ kind: "FilePath", value: "", suggest })).toBe("filePath");
    expect(slotControl({ kind: "FilePath", value: "" })).toBe("field");
  });
});

describe("path helpers", () => {
  it("shows the root-relative path and warns only about a known-missing, non-empty path", () => {
    expect(shownPath("C:/models/x/a.bos", status("", true, "x/a.bos"))).toBe("x/a.bos");
    expect(shownPath("D:/other/a.bos", status("", true))).toBe("D:/other/a.bos");
    expect(isMissing("a.bos", status("a.bos", false))).toBe(true);
    expect(isMissing("a.bos", null)).toBe(false);
    expect(isMissing("", status("", false))).toBe(false);
  });

  it("starts browsing in the file's folder under a root, else at the roots", () => {
    expect(startFolder("C:/models/x/a.bos", status("", true, "x/a.bos"))).toBe("C:/models/x");
    expect(startFolder("D:/other/a.bos", status("", true))).toBe("");
    expect(startFolder("", null)).toBe("");
  });
});

describe("FilePathControls", () => {
  it("shows the relative path, warns with a badge when the file is missing, and commits a typed path", async () => {
    const commit = vi.fn();
    const files = provider((p) => p.endsWith("a.bos") ? status(p, true, "a.bos") : status(p, false));
    const controls = new FilePathControls(suggestions, commit, { files: () => files });
    const el = controls.get("n1", "path", `${ROOT}/a.bos`, ".bos");
    await tick();
    const input = el.querySelector("input")!;
    const badge = el.querySelector("span")!;
    expect(input.value).toBe("a.bos");
    expect(input.title).toBe(`${ROOT}/a.bos`);
    expect(badge.hidden).toBe(true);

    input.value = "D:/nowhere/b.bos";
    input.dispatchEvent(new Event("change"));
    await tick();
    expect(commit).toHaveBeenCalledWith("n1", "path", "D:/nowhere/b.bos");
    expect(badge.hidden).toBe(false);
    expect(badge.title).toContain("D:/nowhere/b.bos");
    controls.prune(new Set());
  });

  it("opens the browser at the roots, descends into a folder, and sets the picked file", async () => {
    const commit = vi.fn();
    const listings: Record<string, FolderListing> = {
      "": { dir: "", roots: [ROOT], entries: [{ name: ROOT, path: ROOT, isDirectory: true }] },
      [ROOT]: { dir: ROOT, parent: "", roots: [ROOT], entries: [
        { name: "a.bos", path: `${ROOT}/a.bos`, isDirectory: false, sizeBytes: 2048 }] },
    };
    const files = provider((p) => status(p, true, "a.bos"), listings);
    const controls = new FilePathControls(suggestions, commit, { files: () => files });
    const el = controls.get("n1", "path", "", ".bos");
    document.body.append(el);
    el.querySelector("button")!.click();
    await tick();
    const dialog = document.querySelector("[role=dialog]")!;
    const folder = [...dialog.querySelectorAll("div")].find((d) => d.textContent?.startsWith(ROOT + "/"))!;
    (folder as HTMLElement).click();
    await tick();
    const file = [...dialog.querySelectorAll("div")].find((d) => d.textContent?.startsWith("a.bos"))!;
    expect(file.textContent).toContain("2 KB");
    (file as HTMLElement).click();
    expect(commit).toHaveBeenCalledWith("n1", "path", `${ROOT}/a.bos`);
    expect(document.querySelector("[role=dialog]")).toBeNull();
    controls.prune(new Set());
  });

  it("hides the browse button and the badge when the host has no file endpoints", async () => {
    const controls = new FilePathControls(suggestions, vi.fn());
    const el = controls.get("n1", "path", "gone.bos", ".bos");
    await tick();
    expect(el.querySelector("button")!.hidden).toBe(true);
    expect(el.querySelector("span")!.hidden).toBe(true);
    controls.prune(new Set());
  });
});
