// Persisted canvas-theme choice (localStorage), validated against the theme
// seam so a stale or foreign value falls back to the default. Each look
// keeps its own choice under its own key with its own fallback (a chrome's
// themePrefs, chrome.ts), so switching pages does not carry a theme over.

import {
  defaultCanvasTheme,
  isCanvasThemeName,
  type CanvasThemeName,
} from "@bimopenflow/graph";
import { readPref, writePref } from "./prefs.js";

export interface ThemePrefs {
  readonly key: string;
  readonly fallback: CanvasThemeName;
}

export const THEME_PREF_KEY = "bof-app-canvas-theme";

/** The classic look's choice: the key of today, falling back to the seam's default. */
export const CLASSIC_THEME_PREFS: ThemePrefs = { key: THEME_PREF_KEY, fallback: defaultCanvasTheme };

export function loadThemeChoice(prefs: ThemePrefs = CLASSIC_THEME_PREFS): CanvasThemeName {
  const value = readPref(prefs.key);
  // "platoflow-light" was renamed to "light"; migrate stored choices.
  const migrated = value === "platoflow-light" ? "light" : value;
  return migrated !== null && isCanvasThemeName(migrated) ? migrated : prefs.fallback;
}

export function saveThemeChoice(name: CanvasThemeName, prefs: ThemePrefs = CLASSIC_THEME_PREFS): void {
  writePref(prefs.key, name);
}
