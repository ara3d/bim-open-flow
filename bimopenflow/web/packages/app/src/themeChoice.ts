// Persisted canvas-theme choice (localStorage), validated against the theme
// seam so a stale or foreign value falls back to the default.

import {
  defaultCanvasTheme,
  isCanvasThemeName,
  type CanvasThemeName,
} from "./canvasTheme.js";
import { readPref, writePref } from "./prefs.js";

export const THEME_PREF_KEY = "bof-app-canvas-theme";

export function loadThemeChoice(): CanvasThemeName {
  const value = readPref(THEME_PREF_KEY);
  // "platoflow-light" was renamed to "light"; migrate stored choices.
  const migrated = value === "platoflow-light" ? "light" : value;
  return migrated !== null && isCanvasThemeName(migrated) ? migrated : defaultCanvasTheme;
}

export function saveThemeChoice(name: CanvasThemeName): void {
  writePref(THEME_PREF_KEY, name);
}
