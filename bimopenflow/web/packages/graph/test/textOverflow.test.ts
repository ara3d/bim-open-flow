// @vitest-environment node
// TKT-125: no text the canvas paints on a card runs past the card's edges.
// Every sample graph of the graph tool's own (FLOW_SAMPLE_DIRS), and one graph
// holding every kind in the generic committed node catalog, is rendered
// headless by a recording painter (scripts/cardOverflow.ts),
// three ways: as the page shows it, with every card pressed to its least width,
// and with long values in every text param; and at each description level
// (TKT-278), since the description block changes the card's height.
// studio-web runs the same check over the toolkit's samples and catalog.

import { afterAll, describe, expect, it } from "vitest";
import { catalogOverflows, sampleOverflows } from "../scripts/cardOverflow.js";
import { committedCatalog, sampleGraphs } from "../scripts/sampleGraphs.js";
import { defaultDescriptionLevel, descriptionLevels, setDescriptionLevel } from "../src/descriptionLevel.js";

afterAll(() => setDescriptionLevel(defaultDescriptionLevel));

describe("no text overflows its card", () => {
  for (const level of descriptionLevels) {
    it(`with ${level} descriptions, for every sample graph as shown and at least width`, () => {
      setDescriptionLevel(level);
      expect(sampleOverflows(sampleGraphs(), committedCatalog())).toEqual([]);
    });

    it(`with ${level} descriptions, for every catalog kind with default and long values`, () => {
      setDescriptionLevel(level);
      expect([false, true].flatMap((long) => catalogOverflows(committedCatalog(), long))).toEqual([]);
    });
  }
});
