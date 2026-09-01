// The canvas theme seam: the topbar (shell) offers these names and calls
// CanvasEditor.setTheme; the canvas side owns what each name means.
//
// Each canvas theme is a gratify token palette (registered under a "bof-"
// prefixed gratify theme name so gratify's builtins stay untouched) plus the
// per-part colors canvasParts needs that are not gratify tokens (status dots,
// wire colors, selection/snap highlights). gratify's setTheme retargets the
// live tokens and cross-fades; the extras switch instantly via canvasColors().

import { calpha, rgb, setTheme as setGratifyTheme, themes, tokens, type Color, type Tokens } from "gratify";
import type { NodeStatus } from "@bimopenflow/contracts";

export const canvasThemeNames = ["light", "dark"] as const;

export type CanvasThemeName = (typeof canvasThemeNames)[number];

export const defaultCanvasTheme: CanvasThemeName = "light";

export const isCanvasThemeName = (value: string): value is CanvasThemeName =>
  (canvasThemeNames as readonly string[]).includes(value);

type Palette = Omit<Tokens, "mix">;

/** Per-part colors the canvas draws that are not gratify tokens. */
export interface CanvasExtraColors {
  readonly status: Record<NodeStatus, Color>;
  /** Base wire stroke (dark keeps the accent blue; light uses a platoflow-style
   *  desaturated mid-gray so wires stay quiet on the cream canvas). */
  readonly wire: Color;
  /** Blended into the wire as selection strength rises. */
  readonly wireSelected: Color;
  /** Under-stroke drawn behind every wire for separation. */
  readonly wireShadow: Color;
  /** Rubber wire once snapped to a valid socket. */
  readonly rubberSnap: Color;
  /** Background grid dot (alpha included). */
  readonly gridDot: Color;
}

export interface CanvasTheme {
  readonly palette: Palette;
  readonly extras: CanvasExtraColors;
}

// "dark" = gratify's builtin dark palette (the canvas's original look).
const darkPalette: Palette = {
  bg: rgb(18, 20, 26),
  surface: rgb(36, 40, 52),
  surfaceHi: rgb(52, 58, 74),
  muted: rgb(70, 76, 94),
  text: rgb(206, 212, 224),
  textDim: rgb(130, 138, 156),
  textBright: rgb(242, 246, 252),
  accent: rgb(64, 186, 255),
  accent2: rgb(168, 130, 255),
  danger: rgb(255, 92, 108),
};

// "light" = platoflow/web/src/theme.ts cream palette, token for token.
const lightPalette: Palette = {
  bg: rgb(239, 237, 232),
  surface: rgb(255, 255, 255),
  surfaceHi: rgb(243, 241, 236),
  muted: rgb(213, 210, 203),
  text: rgb(45, 44, 40),
  textDim: rgb(138, 136, 128),
  textBright: rgb(26, 26, 24),
  accent: rgb(26, 26, 24),
  accent2: rgb(138, 136, 128),
  danger: rgb(192, 57, 43),
};

export const canvasThemes: Record<CanvasThemeName, CanvasTheme> = {
  light: {
    palette: lightPalette,
    extras: {
      status: {
        Ok: rgb(46, 140, 74),
        Unready: rgb(138, 136, 128),
        EffectPending: rgb(184, 122, 20),
        Unavailable: rgb(96, 116, 146),
        Error: rgb(192, 57, 43),
      },
      // platoflow contracts.ts "hue whisper" grays: scene #7A98A8 as the base.
      wire: rgb(122, 152, 168),
      wireSelected: rgb(191, 131, 26),
      wireShadow: calpha(rgb(0, 0, 0), 0.1),
      rubberSnap: rgb(34, 160, 80),
      gridDot: calpha(rgb(190, 186, 178), 0.9),
    },
  },
  dark: {
    palette: darkPalette,
    extras: {
      status: {
        Ok: rgb(59, 165, 93),
        Unready: rgb(150, 148, 140),
        EffectPending: rgb(217, 154, 43),
        Unavailable: rgb(120, 140, 170),
        Error: rgb(192, 57, 43),
      },
      wire: darkPalette.accent,
      wireSelected: rgb(255, 200, 80),
      wireShadow: calpha(rgb(0, 0, 0), 0.25),
      rubberSnap: rgb(90, 220, 130),
      gridDot: calpha(darkPalette.muted, 0.3),
    },
  },
};

const gratifyThemeName = (name: CanvasThemeName): string => `bof-${name}`;

for (const name of canvasThemeNames)
  themes[gratifyThemeName(name)] = canvasThemes[name].palette;

let current: CanvasThemeName = defaultCanvasTheme;

/** Extras for the active canvas theme; parts read this each render. */
export const canvasColors = (): CanvasExtraColors => canvasThemes[current].extras;

export const currentCanvasTheme = (): CanvasThemeName => current;

/** Retarget gratify's live tokens (cross-fade) and swap the extras. With
 *  `instant`, the live tokens snap to the palette immediately — used at boot,
 *  where fading in from gratify's builtin dark would paint the first frames a
 *  washed mid-fade gray (and a throttled background tab could sit on that
 *  frame indefinitely). User-initiated switches keep the cross-fade. */
export function applyCanvasTheme(name: CanvasThemeName, instant = false): void {
  current = name;
  setGratifyTheme(gratifyThemeName(name));
  if (instant) {
    const palette = canvasThemes[name].palette;
    for (const key of Object.keys(palette) as (keyof Palette)[])
      Object.assign(tokens[key], palette[key]);
  }
}
