// The long-text parameter row: a painted label plus a one-line preview of
// the value. Pressing the row opens `longValueEditor.ts`'s multi-line editor
// as a gratify island (a real DOM element pinned to a world-space rectangle)
// anchored just under the row, so it tracks pan, zoom, and node moves the
// same way the inline fields in canvasControls.ts do. See "The long-value
// editor" in docs/plans/remove-properties-panel.md.
//
// One editor instance lives per row key (nodeId::name, from slotShared's
// islandKey), the same lifetime rule canvasControls.ts uses for its inline
// islands: created lazily, reused across frames, pruned when the row goes
// away. `island()` runs every frame, so `editorFor` only calls `open()` on
// the transition into "open" — calling it every frame would overwrite
// whatever the user is mid-typing.

import { Element, GNode, Press, part, rect, v, Color, fitText } from "gratify";
import type { CanvasIntent } from "./canvasIntents.js";
import { FIELD_SLOT_H, previewText, type SlotContext } from "./canvasSlots.js";
import { createLongValueEditor, type LongValueEditor } from "./longValueEditor.js";
import { paramLabel } from "./numericParam.js";
import { dispatchInline, islandKey } from "./slotShared.js";

const LABEL_SIZE = 14;
const VALUE_SIZE = 14;

/** Padding inside the preview box, both sides, subtracted from its width
 *  before fitText measures how much text fits. */
const PREVIEW_PAD = 14;

interface LongSlotStyle {
  label: Color;
  field: Color;
  edge: Color;
  value: Color;
}

/** One editor per row key, same lifetime rule as canvasControls.ts's islands. */
const editors = new Map<string, LongValueEditor>();

/** Lazily creates the row's editor and opens it exactly once per opening. */
function editorFor(ctx: SlotContext): LongValueEditor {
  const key = islandKey(ctx.nodeId, ctx.param.name);
  let editor = editors.get(key);
  if (!editor) {
    editor = createLongValueEditor(document);
    editors.set(key, editor);
  }
  if (!editor.isOpen()) {
    const { nodeId, param } = ctx;
    editor.open({
      label: `${paramLabel(param.name, param.kind, param.control)} (${param.kind})`,
      value: param.value,
      onCommit: (value) =>
        dispatchInline({ kind: "setParam", nodeId, name: param.name, value } satisfies CanvasIntent),
      onClose: () => dispatchInline({ kind: "closeEditor" } satisfies CanvasIntent),
    });
  }
  return editor;
}

const LongSlot = part<SlotContext, LongSlotStyle>("bof-slot-long", {
  size: (p) => v(p.w, FIELD_SLOT_H),
  style: (t, ch) => ({
    label: t.mix(t.textDim, t.text, ch.hover),
    field: t.mix(t.bg, t.surfaceHi, 0.35 + 0.25 * ch.hover),
    edge: t.mix(t.muted, t.accent, 0.6 * ch.hover),
    value: t.mix(t.text, t.textBright, ch.hover),
  }),
  render(node, painter, style) {
    const r = node.rect;
    const { param } = node.props;
    painter.label(paramLabel(param.name, param.kind, param.control), v(r.x, r.y + 6), style.label, {
      align: "left",
      size: LABEL_SIZE,
    });
    const box = rect(r.x, r.y + 14, r.w, r.h - 16);
    painter.box(box, 5, style.field, style.edge, 1);
    // previewText(..., Infinity) only collapses whitespace runs to one space
    // and trims; fitText then cuts to the box's actual width in px, instead
    // of a fixed character count that was about 100px past the node edge on
    // a wide row and wasted space on a narrow one.
    const collapsed = previewText(param.value, Infinity);
    const fitted = fitText(painter.measure, collapsed, box.w - PREVIEW_PAD, VALUE_SIZE);
    painter.label(fitted || "—", v(box.x + 7, box.center.y), style.value, {
      align: "left",
      size: VALUE_SIZE,
    });
  },
  on: [
    Press((node: GNode<SlotContext>) =>
      ({ kind: "openEditor", nodeId: node.props.nodeId, name: node.props.param.name }) satisfies CanvasIntent),
  ],
  island(node) {
    const ctx = node.props;
    const key = islandKey(ctx.nodeId, ctx.param.name);
    if (!ctx.open) {
      // The canvas (not the user) closed this row — close silently so a
      // later re-open loads a fresh value instead of a stale draft.
      editors.get(key)?.close();
      return null;
    }
    const editor = editorFor(ctx);
    const r = node.rect;
    return { el: editor.el, rect: rect(r.x, r.bottom + 4, Math.max(r.w, 360), 200) };
  },
});

/** Label + painted preview; Press -> {kind:"openEditor"}; when ctx.open, its
 *  island is the editor placed at rect(r.x, r.bottom + 4, max(r.w, 360), 200).
 *  Commits dispatch {kind:"setParam"} then {kind:"closeEditor"} through
 *  dispatchInline. */
export function longTextSlot(ctx: SlotContext): Element {
  return LongSlot(ctx.param.name, ctx);
}

/** Disposes editors whose islandKey is not live. */
export function pruneLongValueEditors(liveKeys: ReadonlySet<string>): void {
  for (const [key, editor] of editors) {
    if (!liveKeys.has(key)) {
      editor.dispose();
      editors.delete(key);
    }
  }
}
