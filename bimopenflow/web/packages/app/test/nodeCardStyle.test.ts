// TKT-277: the chip is the only node card style. Neither look offers a
// style picker, and a preference an earlier version stored is ignored.

import { afterEach, beforeAll, describe, expect, it } from "vitest";
import { studioChrome } from "../src/studio/studioChrome.js";
import { chainDocument, fakeApi, mountApp, settle, stubCanvas, unmountApps } from "./appHarness.js";

beforeAll(stubCanvas);
afterEach(() => { unmountApps(); localStorage.clear(); });

describe("node card style (TKT-277)", () => {
  for (const [look, options] of [["classic", {}], ["studio", { chrome: studioChrome() }]] as const) {
    it(`the ${look} look mounts with a stored style from before and offers no style picker`, async () => {
      localStorage.setItem("bof-app-node-style", "banner");
      const { root } = mountApp(fakeApi({ flow: chainDocument("a", "b") }).api, { initialAnalysis: "flow", ...options });
      await settle();
      expect(root.querySelector('select[aria-label="Node style"]')).toBeNull();
      expect(root.querySelector('select[aria-label="Canvas theme"]')).not.toBeNull();
    });
  }
});
