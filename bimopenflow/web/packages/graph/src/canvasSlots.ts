// Parameter slot control selection and heights. Pure geometry — gratify-free,
// shared by the view model (node heights) and the canvas parts (slot rects).
//
// slotControl(param) returns one of SlotControl's eight types based on the
// descriptor's control.kind (sortColumn, slider, range), KIND_CONTROL[kind],
// and a length-based rule for long Text. CONTROL_HEIGHT[type] gives each
// row's height. Together they replace four scatter locations that used to list
// kinds (INLINE set, slotHeight switch, placeSlots ternary, canvasControls
// if/switch chain).

import type { ControlDescriptor, ParamDescriptor, ParamKind, SuggestDescriptor } from "@bimopenflow/contracts";

export interface CanvasParam {
  readonly descending?: boolean;
  readonly name: string;
  readonly kind: ParamKind;
  readonly value: string;
  readonly enumValues?: readonly string[];
  readonly suggest?: SuggestDescriptor;
  readonly control?: ControlDescriptor;
}

/** Single compact row: label left, control right. */
export const COMPACT_SLOT_H = 32;
/** Caption line + full-width input. */
export const FIELD_SLOT_H = 50;
export const WIDGET_SLOT_H = 70;
/** Wrapped lines a long-text row (SQL, Expression, long plain Text) shows
 *  before cutting the last one with an ellipsis (TKT-82: one truncated line
 *  read as broken for a multi-line SELECT). */
export const LONG_TEXT_LINES = 6;
const LONG_TEXT_LINE_H = 16;
/** Caption line + up to LONG_TEXT_LINES wrapped lines + padding. */
export const LONG_TEXT_SLOT_H = 22 + LONG_TEXT_LINES * LONG_TEXT_LINE_H + 8;

/** The control a parameter row shows. TKT-23 adds members (swatch, typed slider, column picker). */
export type SlotControl =
  | "toggle" | "dropdown" | "number" | "slider" | "range" | "columnSelect" | "field" | "longText";

/** Default control per kind. The Record forces a row for every ParamKind. */
export const KIND_CONTROL = {
  Boolean: "toggle", Enum: "dropdown",
  Integer: "number", Number: "number", Fraction: "number", Percent: "number",
  Text: "field", FilePath: "field", DateTime: "field", ModelRef: "field",
  Json: "longText", Expression: "longText",
} as const satisfies Record<ParamKind, SlotControl>;

/** Control per descriptor.kind. Checked before KIND_CONTROL. */
export const DESCRIPTOR_CONTROL = {
  sortColumn: "columnSelect",
  slider: "slider",
  range: "range",
} as const satisfies Record<ControlDescriptor["kind"], SlotControl>;

/** Row height per control: compact 32, field 50, widget 70. */
export const CONTROL_HEIGHT: Readonly<Record<SlotControl, number>> = {
  toggle: COMPACT_SLOT_H,
  dropdown: COMPACT_SLOT_H,
  number: COMPACT_SLOT_H,
  columnSelect: COMPACT_SLOT_H,
  field: FIELD_SLOT_H,
  longText: LONG_TEXT_SLOT_H,
  slider: WIDGET_SLOT_H,
  range: WIDGET_SLOT_H,
};

/** Plain Text longer than this, or with a line break, gets the long-text row. */
export const LONG_TEXT_CHARS = 60;

/** Descriptor control first (DESCRIPTOR_CONTROL), then the long-Text rule
 *  (Text, no suggest, no control), then KIND_CONTROL. Unknown kind -> "field". */
export function slotControl(
  param: Pick<CanvasParam, "kind" | "value" | "control" | "suggest">,
): SlotControl {
  if (param.control?.kind && param.control.kind in DESCRIPTOR_CONTROL) {
    return DESCRIPTOR_CONTROL[param.control.kind as keyof typeof DESCRIPTOR_CONTROL];
  }
  if (
    param.kind === "Text" &&
    !param.suggest &&
    !param.control &&
    (param.value.includes("\n") || param.value.length > LONG_TEXT_CHARS)
  )
    return "longText";
  return (KIND_CONTROL as Readonly<Partial<Record<ParamKind, SlotControl>>>)[param.kind] ?? "field";
}

/** Replaces slotHeight(kind). */
export function slotHeight(
  param: Pick<CanvasParam, "kind" | "value" | "control" | "suggest">,
): number {
  return CONTROL_HEIGHT[slotControl(param)];
}

/** Whitespace runs (including line breaks) collapsed to one space, trimmed,
 *  cut to maxChars with a trailing "…". */
export function previewText(value: string, maxChars: number): string {
  const collapsed = value.replace(/\s+/g, " ").trim();
  if (collapsed.length <= maxChars) return collapsed;
  return collapsed.slice(0, maxChars - 1) + "…";
}

/** What a slot factory receives for one parameter row. */
export interface SlotContext {
  readonly nodeId: string;
  readonly param: CanvasParam;
  /** Row width in world pixels (node width minus side padding). */
  readonly w: number;
  /** True when CanvasModel.openEditor names this row. */
  readonly open: boolean;
}

export const SLOT_GAP = 4;
export const SLOTS_PAD_TOP = 8;
export const SLOTS_PAD_BOTTOM = 10;
export const SLOT_X_PAD = 10;

export interface SlotPlacement {
  readonly param: CanvasParam;
  /** Offset of the slot top from the node's top edge. */
  readonly y: number;
  readonly h: number;
}

/** Inline params of a node, in catalog order, with document values applied.
 *  Every kind shows on the card; only a `hidden` control descriptor holds a
 *  parameter off it (open question 1 in the plan: agents, MCP, and the
 *  document can still change it, but only the node author's own controls
 *  edit it on the canvas). */
export function inlineParams(
  params: readonly ParamDescriptor[],
  values: Readonly<Record<string, string>>,
): CanvasParam[] {
  return params
    .filter((p) => p.control?.kind !== "hidden")
    .map((p) => ({
      name: p.name,
      kind: p.kind,
      value: values[p.name] ?? p.default,
      ...(p.control?.kind === "sortColumn" ? { descending: values['descending' + p.name] === 'true' } : {}),
      ...(p.enumValues ? { enumValues: p.enumValues } : {}),
      ...(p.suggest ? { suggest: p.suggest } : {}),
      ...(p.control ? { control: p.control } : {}),
    }));
}

/** Stacks the slots below `topOffset` (the bottom of the port rows). */
export function placeSlots(
  params: readonly CanvasParam[],
  topOffset: number,
): { slots: SlotPlacement[]; bottom: number } {
  if (params.length === 0) return { slots: [], bottom: topOffset };
  let y = topOffset + SLOTS_PAD_TOP;
  const slots = params.map((param) => {
    const h = CONTROL_HEIGHT[slotControl(param)];
    const placed = { param, y, h };
    y += h + SLOT_GAP;
    return placed;
  });
  return { slots, bottom: y - SLOT_GAP + SLOTS_PAD_BOTTOM };
}
