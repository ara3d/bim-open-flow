// The canvas theme seam: the topbar (shell) offers these names and calls
// CanvasEditor.setTheme; the canvas side owns what each name means.
export const canvasThemeNames = ["dark", "platoflow-light"] as const;

export type CanvasThemeName = (typeof canvasThemeNames)[number];

export const defaultCanvasTheme: CanvasThemeName = "dark";

export const isCanvasThemeName = (value: string): value is CanvasThemeName =>
  (canvasThemeNames as readonly string[]).includes(value);
