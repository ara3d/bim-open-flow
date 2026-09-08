// Inline parameter slots: which parameter kinds render directly on a canvas
// node, and the vertical layout of those slots. Pure geometry — gratify-free,
// shared by the view model (node heights) and the canvas parts (slot rects).
//
// Slots are deliberately NOT homogeneous: each kind gets the height its
// control needs. Compact kinds (Boolean, Enum, numbers) are a single row with
// the label on the left; text-like kinds (Text, FilePath, DateTime) get a
// caption line plus a full-width input, because values need the width.

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

/** Kinds edited inline on the node. Json/Expression/ModelRef stay in the
 *  params pane: they need more room than a node slot can honestly give. */
const INLINE: ReadonlySet<ParamKind> = new Set([
  "Boolean", "Enum", "Integer", "Number", "Fraction", "Percent", "Text", "FilePath", "DateTime",
]);

export const isInlineKind = (kind: ParamKind): boolean => INLINE.has(kind);

/** Single compact row: label left, control right. */
export const COMPACT_SLOT_H = 32;
/** Caption line + full-width input. */
export const FIELD_SLOT_H = 50;
export const WIDGET_SLOT_H = 70;

export const slotHeight = (kind: ParamKind): number => {
  switch (kind) {
    case "Boolean":
    case "Enum":
    case "Integer":
    case "Number":
    case "Fraction":
    case "Percent":
      return COMPACT_SLOT_H;
    case "Text":
    case "FilePath":
    case "DateTime":
      return FIELD_SLOT_H;
    default:
      return 0;
  }
};

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

/** Inline params of a node, in catalog order, with document values applied. */
export function inlineParams(
  params: readonly ParamDescriptor[],
  values: Readonly<Record<string, string>>,
): CanvasParam[] {
  return params
    .filter((p) => isInlineKind(p.kind) && p.control?.kind !== "hidden")
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
    const h = param.control?.kind === "sortColumn" ? COMPACT_SLOT_H : param.control?.kind === "slider" || param.control?.kind === "range" ? WIDGET_SLOT_H : slotHeight(param.kind);
    const placed = { param, y, h };
    y += h + SLOT_GAP;
    return placed;
  });
  return { slots, bottom: y - SLOT_GAP + SLOTS_PAD_BOTTOM };
}
