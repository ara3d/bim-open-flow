// Inline parameter controls drawn on canvas nodes. Three families, chosen per
// parameter kind for the best editing experience rather than homogeneity:
//
// - BoolSlot: a canvas-drawn toggle (one press, animated, undo-clean).
// - EnumSlot: a canvas-drawn dropdown; the open flag is gratify-local state,
//   the option list is a modal adornment (click-away closes without pressing
//   what is underneath, undo never re-opens it).
// - IslandSlot: a real DOM <input> glued to the node via gratify's island
//   facet — Text/FilePath/DateTime/numbers keep the browser's caret,
//   selection, IME, and (for DateTime) the native picker. Commits on
//   change/Enter, reverts on Escape, so scrubbing keystrokes never floods the
//   undo history.
//
// Island elements are created once per (node, param) and pruned by
// canvasEditor when nodes disappear; intents reach the store through the
// dispatch registered at mount (islands live outside gratify's intent flow).

import {
  at,
  calpha,
  Color,
  css,
  Element,
  GNode,
  Local,
  modal,
  part,
  Press,
  rect,
  themeVersion,
  Tokens,
  v,
} from "gratify";
import type { ParamKind } from "@bimopenflow/contracts";
import type { CanvasParam } from "./canvasSlots.js";
import { COMPACT_SLOT_H, FIELD_SLOT_H } from "./canvasSlots.js";
import { canvasThemes, currentCanvasTheme } from "./canvasTheme.js";
import type { CanvasIntent } from "./canvasIntents.js";
import {
  fromDatetimeLocal,
  normalizeInteger,
  normalizeNumber,
  toDatetimeLocal,
} from "./paramText.js";

const LABEL_SIZE = 10;
const VALUE_SIZE = 11;

// ── Boolean: a toggle switch ─────────────────────────────────────────────────

interface BoolSlotProps {
  nodeId: string;
  name: string;
  value: boolean;
  w: number;
  states?: Record<string, boolean>;
}

interface BoolSlotStyle {
  label: Color;
  track: Color;
  knob: Color;
}

const TOGGLE_W = 26;
const TOGGLE_H = 14;

const BoolSlot = part<BoolSlotProps, BoolSlotStyle>("bof-slot-bool", {
  size: (p) => v(p.w, COMPACT_SLOT_H),
  channels: { on: { target: (n) => (n.props.value ? 1 : 0), rate: 16 } },
  style: (t, ch) => ({
    label: t.mix(t.textDim, t.text, ch.hover),
    track: t.mix(t.mix(t.muted, t.surface, 0.4), t.accent, ch.on ?? 0),
    knob: t.mix(t.text, t.textBright, ch.on ?? 0),
  }),
  render(node, painter, style) {
    const r = node.rect;
    painter.label(node.props.name, v(r.x, r.center.y), style.label, {
      align: "left",
      size: LABEL_SIZE,
    });
    const track = rect(r.right - TOGGLE_W, r.center.y - TOGGLE_H / 2, TOGGLE_W, TOGGLE_H);
    painter.box(track, TOGGLE_H / 2, style.track);
    const on = node.ch.on ?? 0;
    const kx = track.x + TOGGLE_H / 2 + on * (TOGGLE_W - TOGGLE_H);
    painter.dot(v(kx, track.center.y), TOGGLE_H / 2 - 2.5, style.knob);
  },
  on: [
    Press((node: GNode<BoolSlotProps>) =>
      ({
        kind: "setParam",
        nodeId: node.props.nodeId,
        name: node.props.name,
        value: node.props.value ? "false" : "true",
      }) satisfies CanvasIntent),
  ],
});

// ── Enum: a dropdown with a modal option list ────────────────────────────────

interface EnumSlotProps {
  nodeId: string;
  name: string;
  value: string;
  options: readonly string[];
  w: number;
  states?: Record<string, boolean>;
}

type EnumLocal = { open: boolean };

type EnumIntent =
  | { kind: "toggle" }
  | { kind: "close" }
  | { kind: "pick"; value: string };

interface OptionRowProps {
  text: string;
  selected: boolean;
  states?: Record<string, boolean>;
}

const OptionRow = part<OptionRowProps, { fill: Color; text: Color; tick: Color }>(
  "bof-slot-option",
  {
    size: (p, m) => v(Math.max(m.text(p.text, 12).x + 38, 96), 24),
    style: (t, ch, p) => ({
      fill: calpha(t.accent, 0.22 * ch.hover + 0.1 * ch.press),
      text: t.mix(p.selected ? t.accent : t.text, t.textBright, ch.hover),
      tick: t.accent,
    }),
    render(node, painter, style) {
      painter.box(node.rect, 5, style.fill);
      painter.label(node.props.text, v(node.rect.x + 22, node.rect.center.y), style.text, {
        align: "left",
        size: 12,
      });
      if (node.props.selected)
        painter.label("✓", v(node.rect.x + 8, node.rect.center.y), style.tick, {
          align: "left",
          size: 11,
          weight: 700,
        });
    },
    on: [
      Press((node: GNode<OptionRowProps>) =>
        Local<EnumIntent>({ kind: "pick", value: node.props.text })),
    ],
  },
);

const EnumSlot = part<EnumSlotProps, { label: Color; field: Color; edge: Color; value: Color; chevron: Color }>(
  "bof-slot-enum",
  {
    size: (p) => v(p.w, COMPACT_SLOT_H),
    localInit: { open: false } as EnumLocal,
    reduce(local: EnumLocal, intent: EnumIntent, node: GNode<EnumSlotProps>) {
      switch (intent.kind) {
        case "toggle":
          return [{ open: !local.open }] as const;
        case "close":
          return [{ open: false }] as const;
        case "pick":
          return [
            { open: false },
            {
              kind: "setParam",
              nodeId: node.props.nodeId,
              name: node.props.name,
              value: intent.value,
            } satisfies CanvasIntent,
          ] as const;
      }
    },
    channels: { open: { target: (n) => ((n.local as EnumLocal).open ? 1 : 0), rate: 14 } },
    style: (t, ch) => ({
      label: t.mix(t.textDim, t.text, ch.hover),
      field: t.mix(t.bg, t.surfaceHi, 0.35 + 0.25 * ch.hover),
      edge: t.mix(t.muted, t.accent, 0.6 * ch.hover + (ch.open ?? 0)),
      value: t.mix(t.text, t.textBright, ch.hover),
      chevron: t.mix(t.textDim, t.accent, ch.hover + (ch.open ?? 0)),
    }),
    render(node, painter, style) {
      const r = node.rect;
      painter.label(node.props.name, v(r.x, r.center.y), style.label, {
        align: "left",
        size: LABEL_SIZE,
      });
      const field = enumFieldRect(node);
      painter.box(field, 5, style.field, style.edge, 1);
      painter.label(node.props.value || "—", v(field.x + 7, field.center.y), style.value, {
        align: "left",
        size: VALUE_SIZE,
      });
      const c = v(field.right - 11, field.center.y);
      const k = 3.4;
      const dy = k * (1 - 2 * (node.ch.open ?? 0));
      painter.line(v(c.x - k, c.y - dy / 2), v(c.x, c.y + dy / 2), style.chevron, 1.6);
      painter.line(v(c.x, c.y + dy / 2), v(c.x + k, c.y - dy / 2), style.chevron, 1.6);
    },
    on: [Press(() => Local<EnumIntent>({ kind: "toggle" }))],
    adorn(node) {
      if (!(node.local as EnumLocal).open) return [];
      const field = enumFieldRect(node);
      return [
        at(
          modal(
            OptionListPanel(`options`, { gap: 1, pad: 5 },
              node.props.options.map((option) =>
                OptionRow(option, { text: option, selected: option === node.props.value }))),
            Local<EnumIntent>({ kind: "close" }),
          ),
          v(field.x, field.bottom + 3),
        ),
      ];
    },
  },
);

/** The enum's value field: right-aligned, leaving room for the label. */
function enumFieldRect(node: GNode<EnumSlotProps>) {
  const r = node.rect;
  const labelRoom = Math.min(r.w * 0.42, 86);
  const w = r.w - labelRoom;
  return rect(r.right - w, r.y + 1, w, r.h - 2);
}

// ── Island fields: Text, FilePath, DateTime, Integer, Number ─────────────────

type IslandLayout = "compact" | "field";

interface IslandSlotProps {
  nodeId: string;
  name: string;
  paramKind: ParamKind;
  value: string;
  w: number;
  states?: Record<string, boolean>;
}

let dispatchIntent: (intent: CanvasIntent) => void = () => {};

/** canvasEditor registers the live runtime's dispatch here after mount;
 *  island DOM events flow through it into the normal intent path. */
export function setInlineControlDispatch(fn: (intent: CanvasIntent) => void): void {
  dispatchIntent = fn;
}

interface IslandEntry {
  el: HTMLInputElement;
  themeV: number;
  /** Canonical value last pushed into the element (revert target). */
  canonical: string;
  paramKind: ParamKind;
}

const islands = new Map<string, IslandEntry>();

export const islandKey = (nodeId: string, name: string): string => `${nodeId}::${name}`;

/** Drops island elements for (node, param) keys no longer on the canvas. */
export function pruneInlineControls(liveKeys: ReadonlySet<string>): void {
  for (const [key, entry] of islands) {
    if (!liveKeys.has(key)) {
      entry.el.remove();
      islands.delete(key);
    }
  }
}

export function disposeInlineControls(): void {
  pruneInlineControls(new Set());
}

const toInputValue = (kind: ParamKind, canonical: string): string =>
  kind === "DateTime" ? toDatetimeLocal(canonical) : canonical;

/** Input text -> canonical form, or null when invalid (revert). */
function toCanonical(kind: ParamKind, text: string): string | null {
  switch (kind) {
    case "Integer":
      return text.trim() === "" ? "" : normalizeInteger(text);
    case "Number":
      return text.trim() === "" ? "" : normalizeNumber(text);
    case "DateTime":
      return fromDatetimeLocal(text);
    default:
      return text;
  }
}

function styleIsland(el: HTMLInputElement, palette: Omit<Tokens, "mix">): void {
  el.style.cssText =
    "box-sizing:border-box;width:100%;height:100%;border-radius:5px;" +
    "padding:0 7px;font:11px system-ui,'Segoe UI',sans-serif;outline:none;" +
    `border:1px solid ${css(palette.muted)};` +
    `background:${css(palette.bg)};color:${css(palette.text)};`;
  el.style.colorScheme = currentCanvasTheme().includes("light") ? "light" : "dark";
  el.onfocus = () => (el.style.borderColor = css(palette.accent));
  el.onblur = () => (el.style.borderColor = css(palette.muted));
}

function islandFor(props: IslandSlotProps): IslandEntry {
  const key = islandKey(props.nodeId, props.name);
  let entry = islands.get(key);
  if (!entry) {
    const el = document.createElement("input");
    el.type = props.paramKind === "DateTime" ? "datetime-local" : "text";
    if (props.paramKind === "Integer" || props.paramKind === "Number")
      el.inputMode = "decimal";
    el.spellcheck = false;
    el.value = toInputValue(props.paramKind, props.value);
    entry = { el, themeV: -1, canonical: props.value, paramKind: props.paramKind };
    const commit = () => {
      const canonical = toCanonical(entry!.paramKind, el.value);
      if (canonical === null || canonical === entry!.canonical) {
        el.value = toInputValue(entry!.paramKind, entry!.canonical); // revert
        return;
      }
      entry!.canonical = canonical;
      dispatchIntent({ kind: "setParam", nodeId: props.nodeId, name: props.name, value: canonical });
    };
    el.addEventListener("change", commit);
    el.addEventListener("keydown", (e) => {
      if (e.key === "Enter") el.blur();
      if (e.key === "Escape") {
        el.value = toInputValue(entry!.paramKind, entry!.canonical);
        el.blur();
        e.stopPropagation();
      }
    });
    islands.set(key, entry);
  }
  // External changes (undo, another editor) flow in unless the user is typing.
  if (entry.el.ownerDocument.activeElement !== entry.el && entry.canonical !== props.value) {
    entry.canonical = props.value;
    entry.el.value = toInputValue(props.paramKind, props.value);
  }
  if (entry.themeV !== themeVersion) {
    entry.themeV = themeVersion;
    styleIsland(entry.el, canvasThemes[currentCanvasTheme()].palette);
  }
  return entry;
}

const COMPACT_INPUT_W = 96;

const IslandSlot = part<IslandSlotProps, { label: Color }>("bof-slot-island", {
  size: (p) => v(p.w, layoutOf(p.paramKind) === "compact" ? COMPACT_SLOT_H : FIELD_SLOT_H),
  style: (t, ch) => ({ label: t.mix(t.textDim, t.text, ch.hover) }),
  render(node, painter, style) {
    const r = node.rect;
    if (layoutOf(node.props.paramKind) === "compact") {
      painter.label(node.props.name, v(r.x, r.center.y), style.label, {
        align: "left",
        size: LABEL_SIZE,
      });
    } else {
      painter.label(node.props.name, v(r.x, r.y + 6), style.label, {
        align: "left",
        size: LABEL_SIZE,
      });
    }
  },
  island(node) {
    const r = node.rect;
    const compact = layoutOf(node.props.paramKind) === "compact";
    const inputRect = compact
      ? rect(r.right - COMPACT_INPUT_W, r.y + 1, COMPACT_INPUT_W, r.h - 2)
      : rect(r.x, r.y + 14, r.w, r.h - 16);
    return { el: islandFor(node.props).el, rect: inputRect };
  },
});

const layoutOf = (kind: ParamKind): IslandLayout =>
  kind === "Integer" || kind === "Number" ? "compact" : "field";

// ── Slot factory: one element per inline param, chosen by kind ───────────────

export function slotElement(nodeId: string, param: CanvasParam, w: number): Element {
  switch (param.kind) {
    case "Boolean":
      return BoolSlot(param.name, {
        nodeId,
        name: param.name,
        value: param.value === "true",
        w,
      });
    case "Enum":
      return EnumSlot(param.name, {
        nodeId,
        name: param.name,
        value: param.value,
        options: param.enumValues ?? [],
        w,
      });
    default:
      return IslandSlot(param.name, {
        nodeId,
        name: param.name,
        paramKind: param.kind,
        value: param.value,
        w,
      });
  }
}

// ── Option list panel ────────────────────────────────────────────────────────
// A skinned Stack: same layout, plus a surface + border + shadow.

const OptionListPanel = (
  key: string,
  props: { gap?: number; pad?: number },
  children: Element[],
): Element => PanelPart(key, props, children);

const PanelPart = part<{ gap?: number; pad?: number; states?: Record<string, boolean> }, {
  fill: Color;
  edge: Color;
  shadow: Color;
}>("bof-slot-panel", {
  measure: (props, avail, m) => stackMeasure(props, avail, m),
  arrange: (props, r, kids) => stackArrange(props, r, kids),
  style: (t) => ({
    fill: t.surface,
    edge: calpha(t.accent, 0.55),
    shadow: calpha(t.textBright, 0.25),
  }),
  render(node, painter, style) {
    painter.push();
    painter.alpha(0.5 + 0.5 * node.ch.enter);
    painter.glow(style.shadow, 14, () => painter.box(node.rect, 7, style.fill, style.edge, 1));
    painter.pop();
  },
});

// Minimal vertical-stack measure/arrange (gap + pad), local so the panel does
// not depend on Stack's internals.
function stackMeasure(
  props: { gap?: number; pad?: number },
  avail: { x: number; y: number },
  m: { children(avail: { x: number; y: number }): { x: number; y: number }[] },
): { x: number; y: number } {
  const gap = props.gap ?? 0;
  const pad = props.pad ?? 0;
  const sizes = m.children(avail);
  const w = sizes.reduce((mx, s) => Math.max(mx, s.x), 0);
  const h = sizes.reduce((sum, s) => sum + s.y, 0) + gap * Math.max(0, sizes.length - 1);
  return v(w + 2 * pad, h + 2 * pad);
}

function stackArrange(
  props: { gap?: number; pad?: number },
  r: { x: number; y: number; w: number },
  kids: { size: { x: number; y: number } }[],
): ReturnType<typeof rect>[] {
  const gap = props.gap ?? 0;
  const pad = props.pad ?? 0;
  let y = r.y + pad;
  return kids.map(({ size }) => {
    const placed = rect(r.x + pad, y, Math.max(size.x, r.w - 2 * pad), size.y);
    y += size.y + gap;
    return placed;
  });
}
