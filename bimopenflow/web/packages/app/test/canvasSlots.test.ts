import { describe, expect, it } from "vitest";
import type { ParamDescriptor, ParamKind } from "@bimopenflow/contracts";
import {
  COMPACT_SLOT_H,
  CONTROL_HEIGHT,
  FIELD_SLOT_H,
  inlineParams,
  KIND_CONTROL,
  LONG_TEXT_CHARS,
  placeSlots,
  previewText,
  SLOT_GAP,
  SLOTS_PAD_BOTTOM,
  SLOTS_PAD_TOP,
  slotControl,
  slotHeight,
  WIDGET_SLOT_H,
} from "../src/canvasSlots.js";

const p = (name: string, kind: ParamDescriptor["kind"], def = ""): ParamDescriptor => ({
  name,
  kind,
  default: def,
});

describe("inline slot vocabulary", () => {
  it("heights vary by kind: compact rows vs caption+field", () => {
    expect(slotHeight({ kind: "Boolean", value: "" })).toBe(COMPACT_SLOT_H);
    expect(slotHeight({ kind: "Enum", value: "" })).toBe(COMPACT_SLOT_H);
    expect(slotHeight({ kind: "Text", value: "" })).toBe(FIELD_SLOT_H);
    expect(slotHeight({ kind: "FilePath", value: "" })).toBe(FIELD_SLOT_H);
    expect(slotHeight({ kind: "DateTime", value: "" })).toBe(FIELD_SLOT_H);
    expect(slotHeight({ kind: "Json", value: "" })).toBe(FIELD_SLOT_H);
  });
});

describe("KIND_CONTROL", () => {
  it("covers every ParamKind in contracts", () => {
    const kinds: readonly ParamKind[] = [
      "Boolean", "Integer", "Number", "Text", "Enum", "FilePath",
      "ModelRef", "Expression", "Json", "DateTime", "Fraction", "Percent",
    ];
    expect(Object.keys(KIND_CONTROL).sort()).toEqual([...kinds].sort());
  });
});

describe("slotControl", () => {
  it("a slider control descriptor keeps its widget", () => {
    expect(
      slotControl({ kind: "Fraction", value: "0.5", control: { kind: "slider", min: 0, max: 1 } }),
    ).toBe("slider");
  });

  it("multi-line plain Text becomes a long-text row", () => {
    expect(slotControl({ kind: "Text", value: "select *\nfrom doors" })).toBe("longText");
  });

  it("a Text parameter with a suggestion source keeps its field", () => {
    expect(
      slotControl({
        kind: "Text",
        value: "a,b",
        suggest: { kind: "ColumnsOfInput", source: "table" },
      }),
    ).toBe("field");
  });

  it("ModelRef gets a plain field", () => {
    expect(slotControl({ kind: "ModelRef", value: "duplex.bos" })).toBe("field");
  });

  it("an unknown kind falls back to a plain field", () => {
    expect(slotControl({ kind: "Color" as ParamKind, value: "#ff8800" })).toBe("field");
  });

  it("CONTROL_HEIGHT gives every control a row height", () => {
    expect(CONTROL_HEIGHT.toggle).toBe(COMPACT_SLOT_H);
    expect(CONTROL_HEIGHT.dropdown).toBe(COMPACT_SLOT_H);
    expect(CONTROL_HEIGHT.number).toBe(COMPACT_SLOT_H);
    expect(CONTROL_HEIGHT.columnSelect).toBe(COMPACT_SLOT_H);
    expect(CONTROL_HEIGHT.field).toBe(FIELD_SLOT_H);
    expect(CONTROL_HEIGHT.longText).toBe(FIELD_SLOT_H);
    expect(CONTROL_HEIGHT.slider).toBe(WIDGET_SLOT_H);
    expect(CONTROL_HEIGHT.range).toBe(WIDGET_SLOT_H);
  });

  it("plain Text right at LONG_TEXT_CHARS stays a field; one over becomes long-text", () => {
    expect(slotControl({ kind: "Text", value: "a".repeat(LONG_TEXT_CHARS) })).toBe("field");
    expect(slotControl({ kind: "Text", value: "a".repeat(LONG_TEXT_CHARS + 1) })).toBe("longText");
  });
});

describe("previewText", () => {
  it("collapses whitespace runs, including line breaks, to one space", () => {
    expect(previewText('{\n  "a": 1\n}', 20)).toBe('{ "a": 1 }');
  });

  it("cuts to maxChars with a trailing ellipsis", () => {
    expect(previewText("area > 10 AND level = 'L1'", 12)).toBe("area > 10 A…");
  });

  it("returns an empty string unchanged", () => {
    expect(previewText("", 12)).toBe("");
  });
});

describe("inlineParams", () => {
  it("keeps catalog order, applies values over defaults, and shows every kind", () => {
    const params = inlineParams(
      [p("header", "Boolean", "true"), p("rows", "Json"), p("path", "FilePath")],
      { path: "data.csv" },
    );
    expect(params.map((x) => x.name)).toEqual(["header", "rows", "path"]);
    expect(params[0]!.value).toBe("true");
    expect(params[2]!.value).toBe("data.csv");
  });

  it("drops a parameter whose descriptor sets control.kind 'hidden'", () => {
    const hidden: ParamDescriptor = {
      name: "sortBy",
      kind: "Text",
      default: "",
      control: { kind: "hidden" },
    };
    const params = inlineParams([p("header", "Boolean", "true"), hidden], {});
    expect(params.map((x) => x.name)).toEqual(["header"]);
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
