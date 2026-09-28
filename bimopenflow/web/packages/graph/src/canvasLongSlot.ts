// The long-text parameter row: a painted label plus a word-wrapped preview of
// the value, up to LONG_TEXT_LINES rows (TKT-82). Pressing the row opens
// `longValueEditor.ts`'s multi-line editor
// as a gratify island (a real DOM element pinned to a world-space rectangle)
// anchored just under the row, so it tracks pan, zoom, and node moves the
// same way the inline fields in canvasControls.ts do. See "The long-value
// editor" in docs/plans/remove-properties-panel.md.
//
// One editor lives per row key (nodeId::name, from slotShared's islandKey)
// on the row's CanvasInstance, the same lifetime rule canvasControls.ts uses
// for its inline islands: created lazily, reused across frames, pruned when
// the row goes away. `island()` runs every frame, so `editorFor` only calls `open()` on
// the transition into "open" — calling it every frame would overwrite
// whatever the user is mid-typing.

import { Element, GNode, Press, part, rect, v, Color, Measure, themeVersion } from "gratify";
import type { CanvasIntent } from "./canvasIntents.js";
import { LONG_TEXT_LINES, LONG_TEXT_SLOT_H, type SlotContext } from "./canvasSlots.js";
import { canvasThemes, currentCanvasTheme } from "./canvasTheme.js";
import { createLongValueEditor, type LongValueEditor } from "./longValueEditor.js";
import { paramLabel } from "./numericParam.js";
import { islandKey, styleIsland } from "./slotShared.js";
import type { CanvasInstance } from "./instance.js";

const LABEL_SIZE = 14;
const VALUE_SIZE = 14;

/** Padding inside the preview box, both sides, subtracted from its width
 *  before fitText measures how much text fits. */
const PREVIEW_PAD = 14;

const LINE_H = 16;

/** Greedy word-wrap of already-split `words` into at most `maxLines` lines
 *  that each fit `maxW`; the last line gets an ellipsis, shrunk a character
 *  at a time until it fits, when words remain past the cap. Returns the
 *  wrapped lines and whether every word was placed. */
function wrapWords(
  words: readonly string[],
  measure: Measure,
  maxW: number,
  size: number,
  maxLines: number,
): { lines: string[]; complete: boolean } {
  const lines: string[] = [];
  let line = "";
  let i = 0;
  while (i < words.length && lines.length < maxLines) {
    const word = words[i]!;
    const candidate = line ? `${line} ${word}` : word;
    if (line && measure.text(candidate, size).x > maxW) {
      lines.push(line);
      line = "";
      continue;
    }
    line = candidate;
    i++;
  }
  if (line && lines.length < maxLines) lines.push(line);
  const complete = i >= words.length;
  if (!complete) {
    // More words than fit: mark the last line as cut, shrinking it a
    // character at a time until "…" fits inside maxW too.
    let last = lines.pop() ?? "";
    let withEllipsis = `${last}…`;
    while (last.length > 0 && measure.text(withEllipsis, size).x > maxW) {
      last = last.slice(0, -1).trimEnd();
      withEllipsis = `${last}…`;
    }
    lines.push(withEllipsis);
  }
  return { lines, complete };
}

/** Greedy word-wrap of `text` into at most `maxLines` lines that each fit
 *  `maxW`; whitespace runs (including line breaks) collapse to one space
 *  first, so a formatted SQL query wraps by width rather than keeping its
 *  original line breaks. The last line gets an ellipsis when words remain. */
function wrapLines(measure: Measure, text: string, maxW: number, size: number, maxLines: number): string[] {
  const words = text.replace(/\s+/g, " ").trim().split(" ").filter((w) => w.length > 0);
  return wrapWords(words, measure, maxW, size, maxLines).lines;
}

/** Word-wrap that keeps a view.note's paragraph breaks: each "\n"-separated
 *  paragraph wraps on its own, and an empty paragraph keeps its blank line.
 *  Stops at maxLines total, ellipsizing the last line once the text (words or
 *  paragraphs) outruns the cap. */
export function wrapParagraphs(
  measure: Measure,
  text: string,
  maxW: number,
  size: number,
  maxLines: number,
): string[] {
  const paragraphs = text.split("\n");
  const lines: string[] = [];
  for (let p = 0; p < paragraphs.length; p++) {
    if (lines.length >= maxLines) break;
    const words = paragraphs[p]!.trim().split(/\s+/).filter((w) => w.length > 0);
    if (words.length === 0) {
      lines.push("");
      continue;
    }
    const { lines: wrapped, complete } = wrapWords(words, measure, maxW, size, maxLines - lines.length);
    lines.push(...wrapped);
    if (!complete) break;
  }
  return lines.slice(0, maxLines);
}

interface LongSlotStyle {
  label: Color;
  field: Color;
  edge: Color;
  value: Color;
}

/** styleIsland replaces the textarea's whole cssText with the theme's border,
 *  background, and text colour, so the flex sizing longValueEditor.ts's
 *  layout depends on, and the monospace font a JSON or expression value
 *  reads better in, have to be reapplied after it, not instead of it. */
function styleEditorTextarea(editor: LongValueEditor): void {
  const ta = editor.textarea;
  styleIsland(ta, canvasThemes[currentCanvasTheme()].palette);
  ta.style.flex = "1 1 auto";
  ta.style.resize = "none";
  ta.style.fontFamily = "ui-monospace, 'Cascadia Code', monospace";
  ta.style.fontSize = "13px";
}

/** Lazily creates the row's editor and opens it exactly once per opening. */
function editorFor(ctx: SlotContext): LongValueEditor {
  const { instance } = ctx;
  const key = islandKey(ctx.nodeId, ctx.param.name);
  let editor = instance.longEditors.get(key);
  if (!editor) {
    editor = createLongValueEditor(instance.document);
    instance.longEditors.set(key, editor);
  }
  if (instance.longEditorThemeV.get(key) !== themeVersion) {
    instance.longEditorThemeV.set(key, themeVersion);
    styleEditorTextarea(editor);
  }
  if (!editor.isOpen()) {
    const { nodeId, param } = ctx;
    editor.open({
      label: `${paramLabel(param.name, param.kind, param.control)} (${param.kind})`,
      value: param.value,
      onCommit: (value) =>
        instance.dispatch({ kind: "setParam", nodeId, name: param.name, value } satisfies CanvasIntent),
      onClose: () => instance.dispatch({ kind: "closeEditor" } satisfies CanvasIntent),
    });
  }
  return editor;
}

const LongSlot = part<SlotContext, LongSlotStyle>("bof-slot-long", {
  size: (p) => v(p.w, LONG_TEXT_SLOT_H),
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
    if (!param.value) {
      painter.label("—", v(box.x + 7, box.y + LINE_H * 0.7), style.value, { align: "left", size: VALUE_SIZE });
      return;
    }
    // Word-wrap over LONG_TEXT_LINES rows, so a multi-line SQL query or
    // expression reads as more than its first clause (TKT-82).
    const lines = wrapLines(painter.measure, param.value, box.w - PREVIEW_PAD, VALUE_SIZE, LONG_TEXT_LINES);
    lines.forEach((line, i) => {
      painter.label(line, v(box.x + 7, box.y + LINE_H * (i + 0.7)), style.value, {
        align: "left",
        size: VALUE_SIZE,
      });
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
      ctx.instance.longEditors.get(key)?.close();
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

/** Disposes the instance's editors whose islandKey is not live. */
export function pruneLongValueEditors(instance: CanvasInstance, liveKeys: ReadonlySet<string>): void {
  for (const [key, editor] of instance.longEditors) {
    if (!liveKeys.has(key)) {
      editor.dispose();
      instance.longEditors.delete(key);
      instance.longEditorThemeV.delete(key);
    }
  }
}
