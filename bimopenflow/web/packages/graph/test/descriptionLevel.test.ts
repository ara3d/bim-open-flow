// TKT-278: the description level seam and how much of a description each level shows.

import { afterEach, describe, expect, it, vi } from "vitest";
import {
  BRIEF_SENTENCES, currentDescriptionLevel, defaultDescriptionLevel, descriptionLevels, descriptionText,
  isDescriptionLevel, leadSentences, nextDescriptionLevel, onDescriptionLevelChange, setDescriptionLevel,
} from "../src/descriptionLevel.js";

afterEach(() => setDescriptionLevel(defaultDescriptionLevel));

const text = "Emits one row per pair. Boxes come from the model. Rows in no box produce nothing.";

describe("descriptionText", () => {
  it("shows nothing, the first two sentences, or the whole text", () => {
    expect(descriptionText(text, "hidden")).toBe("");
    expect(descriptionText(text, "brief")).toBe("Emits one row per pair. Boxes come from the model.");
    expect(descriptionText(`  ${text}\n`, "full")).toBe(text);
  });

  it("opens at brief, two sentences", () => {
    expect(defaultDescriptionLevel).toBe("brief");
    expect(BRIEF_SENTENCES).toBe(2);
  });
});

describe("leadSentences", () => {
  it("takes the first sentences, ending at . ! ? before a space or at a line end", () => {
    expect(leadSentences(text, 1)).toBe("Emits one row per pair.");
    expect(leadSentences("Sorts rows\nby a column", 1)).toBe("Sorts rows");
    expect(leadSentences("Groups by the given columns", 2)).toBe("Groups by the given columns");
    expect(leadSentences("", 2)).toBe("");
  });
});

describe("the level seam", () => {
  it("cycles hidden, brief, full, hidden", () => {
    expect(descriptionLevels.map(nextDescriptionLevel)).toEqual(["brief", "full", "hidden"]);
  });

  it("accepts only the three names", () => {
    expect(descriptionLevels.every(isDescriptionLevel)).toBe(true);
    expect(isDescriptionLevel("compact")).toBe(false);
  });

  it("notifies once per change and not for the current level", () => {
    const listener = vi.fn();
    const stop = onDescriptionLevelChange(listener);
    setDescriptionLevel("full");
    setDescriptionLevel("full");
    expect(currentDescriptionLevel()).toBe("full");
    expect(listener).toHaveBeenCalledTimes(1);
    stop();
    setDescriptionLevel("hidden");
    expect(listener).toHaveBeenCalledTimes(1);
  });
});
