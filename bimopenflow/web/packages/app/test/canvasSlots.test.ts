import { describe, expect, it } from "vitest";
import type { ParamDescriptor } from "@bimopenflow/contracts";
import {
  COMPACT_SLOT_H,
  FIELD_SLOT_H,
  inlineParams,
  isInlineKind,
  placeSlots,
  SLOT_GAP,
  SLOTS_PAD_BOTTOM,
  SLOTS_PAD_TOP,
  slotHeight,
} from "../src/canvasSlots.js";

const p = (name: string, kind: ParamDescriptor["kind"], def = ""): ParamDescriptor => ({
  name,
  kind,
  default: def,
});

describe("inline slot vocabulary", () => {
  it("simple kinds are inline; heavy kinds stay in the pane", () => {
    for (const kind of ["Boolean", "Enum", "Integer", "Number", "Text", "FilePath", "DateTime"] as const)
      expect(isInlineKind(kind)).toBe(true);
    for (const kind of ["Json", "Expression", "ModelRef"] as const)
      expect(isInlineKind(kind)).toBe(false);
  });

  it("heights vary by kind: compact rows vs caption+field", () => {
    expect(slotHeight("Boolean")).toBe(COMPACT_SLOT_H);
    expect(slotHeight("Enum")).toBe(COMPACT_SLOT_H);
    expect(slotHeight("Text")).toBe(FIELD_SLOT_H);
    expect(slotHeight("FilePath")).toBe(FIELD_SLOT_H);
    expect(slotHeight("DateTime")).toBe(FIELD_SLOT_H);
    expect(slotHeight("Json")).toBe(0);
  });
});

describe("inlineParams", () => {
  it("keeps catalog order, applies values over defaults, drops pane-only kinds", () => {
    const params = inlineParams(
      [p("header", "Boolean", "true"), p("rows", "Json"), p("path", "FilePath")],
      { path: "data.csv" },
    );
    expect(params.map((x) => x.name)).toEqual(["header", "path"]);
    expect(params[0]!.value).toBe("true");
    expect(params[1]!.value).toBe("data.csv");
  });

  it("carries enum options through", () => {
    const desc: ParamDescriptor = {
      name: "mode",
      kind: "Enum",
      default: "left",
      enumValues: ["left", "inner"],
    };
    expect(inlineParams([desc], {})[0]!.enumValues).toEqual(["left", "inner"]);
  });
});

describe("placeSlots", () => {
  it("returns the top offset untouched when there are no slots", () => {
    expect(placeSlots([], 56)).toEqual({ slots: [], bottom: 56 });
  });

  it("stacks variable-height slots with gaps and padding", () => {
    const params = inlineParams(
      [p("header", "Boolean"), p("path", "FilePath")],
      {},
    );
    const { slots, bottom } = placeSlots(params, 56);
    expect(slots[0]!.y).toBe(56 + SLOTS_PAD_TOP);
    expect(slots[0]!.h).toBe(COMPACT_SLOT_H);
    expect(slots[1]!.y).toBe(56 + SLOTS_PAD_TOP + COMPACT_SLOT_H + SLOT_GAP);
    expect(slots[1]!.h).toBe(FIELD_SLOT_H);
    expect(bottom).toBe(slots[1]!.y + FIELD_SLOT_H + SLOTS_PAD_BOTTOM);
  });
});
