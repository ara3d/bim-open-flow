// @vitest-environment node
// TKT-125: no text the canvas paints on a card runs past the card's edges.
// Every sample graph of the graph tool's own (FLOW_SAMPLE_DIRS), and one graph
// holding every kind in the generic committed node catalog, is rendered
// headless in every node style by a recording painter (scripts/cardOverflow.ts),
// three ways: as the page shows it, with every card pressed to its least width,
// and with long values in every text param. studio-web runs the same check over
// the toolkit's samples and catalog.

import { afterAll, describe, expect, it } from "vitest";
import { defaultNodeStyle, nodeStyleNames, setNodeStyle } from "../src/nodeStyle.js";
import { catalogOverflows, sampleOverflows } from "../scripts/cardOverflow.js";
import { committedCatalog, sampleGraphs } from "../scripts/sampleGraphs.js";

afterAll(() => setNodeStyle(defaultNodeStyle));

describe("no text overflows its card", () => {
  for (const style of nodeStyleNames) {
    it(`in the ${style} style, for every sample graph as shown and at least width`, () => {
      setNodeStyle(style);
      expect(sampleOverflows(sampleGraphs(), committedCatalog())).toEqual([]);
    });

    it(`in the ${style} style, for every catalog kind with default and long values`, () => {
      setNodeStyle(style);
      expect([false, true].flatMap((long) => catalogOverflows(committedCatalog(), long))).toEqual([]);
    });
  }
});
