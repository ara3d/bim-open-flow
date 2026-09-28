// Copied from submodules/gratify/examples/shared/ (planned debt in docs/plans/graph-editor-package.md:
// the package must not import gratify's example code; TKT-23 rebuilds the slider and range as
// this package's own parts and deletes this copy).
// Shared example widgets — each one is a single part() definition (README §5).
// These live in examples/ deliberately: the framework ships primitives, apps
// (or a future widget library) ship widgets.

import {
  addOn, calpha, Drag1D, Element, Gesture, GNode, Intentish, Label, part, Press, rect, Row,
  Stack, surface, v, withExt,
} from "gratify";
import {
  dragMax, dragMin, fracOfValue, pxOfValue, RangeZone, shiftWindow, valueOfPx, zoneAt,
} from "./gratifyRangeMath.js";

// ---- Button -----------------------------------------------------------------
// The style record is INFERRED from `style`'s return value — no named interface,
// no second type parameter. `surface(...)` supplies the house { fill, edge, text }
// hover/press blends; the widget adds only its own fields (corner, lift).
export interface ButtonProps {
  label: string;
  press: Intentish;
  accent?: boolean;
  danger?: boolean;
}

export const Button = part<ButtonProps>()("button", {
  size: (props, m) => v(m.text(props.label).x + 28, 32),
  style: (t, ch, props) => ({
    ...surface(t, ch, { tint: props.danger ? t.danger : props.accent ? t.accent : undefined }),
    corner: 8,
    lift: 2 * ch.hover - 2 * ch.press,
  }),
  render: (node, p, s) => {
    const r = node.rect.raise(s.lift);
    p.box(r, s.corner, s.fill, s.edge, 1);
    p.label(node.props.label, r.center, s.text, { weight: 500 });
  },
  on: [Press((node) => node.props.press)],
});

// ---- TimedButton ----------------------------------------------------------------
// The same face as Button, but the intent is built at click time from the
// node's clock — for reducers that record WHEN something happened (idle
// timers, cooldowns, flashes) without reading a clock themselves.
export interface TimedButtonProps {
  label: string;
  to(time: number): Intentish;
  accent?: boolean;
  danger?: boolean;
}

export const TimedButton = part("timed-button")
  .props<TimedButtonProps>()
  .size((props, m) => v(m.text(props.label).x + 28, 32))
  .style((t, ch, props) => ({
    ...surface(t, ch, { tint: props.danger ? t.danger : props.accent ? t.accent : undefined }),
    corner: 8,
    lift: 2 * ch.hover - 2 * ch.press,
  }))
  .render((node, p, s) => {
    const r = node.rect.raise(s.lift);
    p.box(r, s.corner, s.fill, s.edge, 1);
    p.label(node.props.label, r.center, s.text, { weight: 500 });
  })
  .press((node) => node.props.to(node.time ?? 0));

// ---- Checkbox -----------------------------------------------------------------
// With `label`, the text is part of the SAME part — so the whole box-plus-text
// run is one hit target and clicking the words toggles too (the HTML <label>
// courtesy). The label dims through the `on` channel, so checking off
// cross-fades it for free.
export interface CheckboxProps { on: boolean; toggle: Intentish; label?: string; }

export const Checkbox = part<CheckboxProps>()("checkbox", {
  size: (props, m) => (props.label ? v(28 + m.text(props.label, 12).x, 20) : v(20, 20)),
  channels: {
    on: { target: (n: GNode<CheckboxProps>) => (n.props.on ? 1 : 0), spring: { stiffness: 340, damping: 22 } },
  },
  style: (t, ch) => {
    const on = Math.min(1, Math.max(0, ch.on));
    return {
      fill: t.mix(t.surface, t.accent, on * 0.9),
      edge: t.mix(t.muted, t.accent, Math.max(on, ch.hover * 0.6)),
      mark: calpha(t.textBright, on),
      text: t.mix(t.mix(t.text, t.textBright, 0.3 * ch.hover), t.textDim, on),
      pop: ch.on,
    };
  },
  render(node, p, s) {
    const r = node.rect;
    const box = rect(r.x + 1, r.y + 1, 18, 18);
    p.box(box, 6, s.fill, s.edge, 1.5);
    const c = box.center, k = Math.min(1.15, Math.max(0, s.pop));
    if (k > 0.02) {
      p.line(v(c.x - 4 * k, c.y), v(c.x - 1 * k, c.y + 3 * k), s.mark, 2);
      p.line(v(c.x - 1 * k, c.y + 3 * k), v(c.x + 4.5 * k, c.y - 3.5 * k), s.mark, 2);
    }
    if (node.props.label) p.label(node.props.label, v(r.x + 28, r.center.y), s.text, { align: "left", size: 12 });
  },
  on: [Press((node) => node.props.toggle)],
});

// ---- Toggle switch ------------------------------------------------------------
export interface ToggleProps { on: boolean; flip: Intentish; }

export const Toggle = part<ToggleProps>()("toggle", {
  size: () => v(42, 24),
  channels: {
    on: { target: (n: GNode<ToggleProps>) => (n.props.on ? 1 : 0), spring: { stiffness: 260, damping: 20 } },
  },
  style: (t, ch) => ({
    track: t.mix(t.muted, t.accent, Math.min(1, Math.max(0, ch.on))),
    knob: t.textBright,
    travel: ch.on,                    // a spring, so the knob *thunks*
    glow: 8 * ch.hover,
  }),
  render(node, p, s) {
    const r = node.rect;
    p.box(r, r.h / 2, s.track);
    const knobX = r.x + 12 + s.travel * (r.w - 24);
    p.glow(s.track, s.glow, () => p.dot(v(knobX, r.center.y), 8, s.knob));
  },
  on: [Press((node) => node.props.flip)],
});

// ---- Slider ---------------------------------------------------------------------
export interface SliderProps {
  value: number;                       // 0..1
  set(value: number): Intentish;
  width?: number;
}

export const Slider = part<SliderProps>()("slider", {
  size: (props) => v(props.width ?? 170, 30),
  channels: {
    shown: { target: (n: GNode<SliderProps>) => n.props.value, spring: { stiffness: 300, damping: 24 } },
  },
  style: (t, ch) => ({
    track: t.muted,
    fill: t.accent,
    knob: t.mix(t.textBright, t.accent, ch.hover * 0.3),
    knobR: 6.5 + 2 * ch.hover + 1 * ch.press,
    glow: 10 * ch.hover,
  }),
  render(node, p, s) {
    const r = node.rect;
    const x = r.x + 8, w = r.w - 16, y = r.center.y;
    const t = Math.min(1, Math.max(0, node.ch.shown));
    p.box(rect(x, y - 2.5, w, 5), 2.5, s.track);
    p.box(rect(x, y - 2.5, w * t, 5), 2.5, s.fill);
    p.glow(s.fill, s.glow, () => p.dot(v(x + w * t, y), s.knobR, s.knob));
  },
  on: [Drag1D({ axis: "x", to: (node, f) => node.props.set(f) })],
});

// ---- Range (dual-thumb min..max slider) --------------------------------------
// A horizontal track with two thumbs and a filled span between them. Dragging a
// thumb edits min or max (they can't cross); dragging the fill between them
// shifts the whole window, width preserved. Values live in a real domain
// [lo, hi] with an optional step. Emits `set(min, max)` per move — the host
// coalesces (undo middleware, param write-coalescing, etc.). While the pointer
// is down, value labels ride above the thumbs. All the value math is in
// range-math.ts (pure, kernel-tested).
export interface RangeProps {
  min: number;
  max: number;
  lo: number;
  hi: number;
  step?: number;
  set(min: number, max: number): Intentish;
  width?: number;
  /** Label formatting during drag; default trims to the step's precision. */
  fmt?(value: number): string;
}

const PAD = 10;              // track inset — room for thumbs at the extremes
const THUMB = 7;             // thumb radius

const rangeFmt = (props: RangeProps) => (value: number): string => {
  if (props.fmt) return props.fmt(value);
  const step = props.step ?? 0;
  const decimals = step >= 1 ? 0 : step > 0 ? Math.ceil(-Math.log10(step)) : 2;
  return value.toFixed(decimals);
};

interface RangeDrag {
  zone: RangeZone;
  startMin: number;
  startMax: number;
  startX: number;
}

export const Range = part<RangeProps>()("range", {
  size: (props) => v(props.width ?? 170, 34),
  style: (t, ch) => ({
    track: t.muted,
    fill: t.accent,
    thumb: t.mix(t.textBright, t.accent, 0.3 * ch.hover),
    thumbR: THUMB + 1.5 * ch.hover,
    glow: 9 * ch.hover,
    label: t.textBright,
    tag: calpha(t.bg, 0.75),
    drag: ch.press,                  // labels ride the press channel
  }),
  render(node, p, s) {
    const props = node.props;
    const r = node.rect;
    const d = { lo: props.lo, hi: props.hi, step: props.step };
    const x = r.x + PAD, w = r.w - 2 * PAD, y = r.center.y;
    const xMin = pxOfValue(props.min, d, x, w);
    const xMax = pxOfValue(props.max, d, x, w);
    p.box(rect(x, y - 2.5, w, 5), 2.5, s.track);
    p.box(rect(xMin, y - 2.5, Math.max(0, xMax - xMin), 5), 2.5, s.fill);
    for (const tx of [xMin, xMax])
      p.glow(s.fill, s.glow, () => p.dot(v(tx, y), s.thumbR, s.thumb));
    if (s.drag > 0.05) {
      const fmt = rangeFmt(props);
      const labels = [[xMin, fmt(props.min)], [xMax, fmt(props.max)]] as const;
      for (const [tx, text] of labels) {
        const lw = 8 + text.length * 6.5;
        p.box(rect(tx - lw / 2, y - 26, lw, 14), 4, calpha(s.tag, s.drag));
        p.label(text, v(tx, y - 19), calpha(s.label, s.drag), { size: 10, weight: 600 });
      }
    }
  },
  on: [
    Gesture<RangeProps, RangeDrag>({
      begin(node, pointer) {
        const props = node.props;
        const d = { lo: props.lo, hi: props.hi, step: props.step };
        const x = node.rect.x + PAD, w = node.rect.w - 2 * PAD;
        const f = w > 0 ? (pointer.x - x) / w : 0;
        const zone = zoneAt(f, fracOfValue(props.min, d), fracOfValue(props.max, d), THUMB * 1.5 / Math.max(w, 1));
        return { zone, startMin: props.min, startMax: props.max, startX: pointer.x };
      },
      during(st, node, pointer) {
        const props = node.props;
        const d = { lo: props.lo, hi: props.hi, step: props.step };
        const x = node.rect.x + PAD, w = node.rect.w - 2 * PAD;
        const next =
          st.zone === "min" ? dragMin(valueOfPx(pointer.x, d, x, w), props.max, d)
          : st.zone === "max" ? dragMax(props.min, valueOfPx(pointer.x, d, x, w), d)
          : shiftWindow(st.startMin, st.startMax,
              (pointer.x - st.startX) / Math.max(w, 1) * (d.hi - d.lo), d);
        return props.set(next.min, next.max);
      },
    }),
  ],
});

// ---- Icon button (×) ---------------------------------------------------------
export interface IconButtonProps { press: Intentish; }

export const CloseButton = part<IconButtonProps>()("close-button", {
  size: () => v(20, 20),
  style: (t, ch) => ({
    fill: calpha(t.danger, 0.12 + 0.5 * ch.hover),
    x: t.mix(t.textDim, t.danger, ch.hover),
    spin: ch.press,
  }),
  render(node, p, s) {
    const r = node.rect.inset(1), c = r.center, k = 3.6 * (1 - 0.3 * s.spin);
    p.box(r, 6, s.fill);
    p.line(v(c.x - k, c.y - k), v(c.x + k, c.y + k), s.x, 1.8);
    p.line(v(c.x - k, c.y + k), v(c.x + k, c.y - k), s.x, 1.8);
  },
  on: [Press((node) => node.props.press)],
});

// ---- Card (a composite: a part MADE OF parts) --------------------------------
// `body` supplies the chrome — a titled Stack — and drops the use-site children
// into a content slot. `render` paints the card background UNDER that content
// (drawPass renders a part before its children). One definition; every card in
// the app is now themable and restylable through this single seam.
export interface CardProps { title: string; value?: string; }

export const Card = part<CardProps>()("card", {
  style: (t, ch) => ({
    fill: t.mix(t.surface, t.surfaceHi, 0.35 + 0.4 * ch.hover),
    edge: t.mix(t.muted, t.accent, 0.2 + 0.5 * ch.hover),
    corner: 10,
  }),
  render: (node, p, s) => p.box(node.rect, s.corner, s.fill, s.edge, 1),
  body: (props, children): Element[] => [
    Stack("layout", { pad: 14, gap: 10, align: "stretch" }, [
      Row("head", { gap: 8, justify: "between" }, [
        Label("title", { text: props.title, weight: 600, size: 12 }),
        ...(props.value ? [Label("value", { text: props.value, dim: true, size: 11 })] : []),
      ]),
      ...children,
    ]),
  ],
});

// ---- Labeled (the rung-1 alternative: a plain function, no framework) --------
// When you only need a private arrangement, a function suffices — no named part,
// no theme seam. Reach for `Card` (a part) when you want the arrangement to be
// named, themable, and reachable by `extendTheme("dark", "card", …)`.
// Pass `press` to make the caption clickable (toggle rows: clicking the words
// should act like clicking the widget) — a one-line use-site extension.
export const Labeled = (key: string, text: string, el: Element, press?: Intentish): Element =>
  Row(key, { gap: 8, align: "center" }, [
    press === undefined
      ? Label(`${key}/l`, { text, dim: true, size: 11 })
      : withExt(Label(`${key}/l`, { text, dim: true, size: 11 }), addOn(Press(() => press))),
    el,
  ]);
