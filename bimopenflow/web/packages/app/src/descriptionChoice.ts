// Persisted description level (localStorage, TKT-278), validated against the
// graph package's level seam so a stale or foreign value falls back to the
// default. One choice per browser, shared by every look: the themeChoice.ts
// pattern without the per-look key.

import { defaultDescriptionLevel, isDescriptionLevel, type DescriptionLevel } from "@bimopenflow/graph";
import { readPref, writePref } from "./prefs.js";

export const DESCRIPTION_PREF_KEY = "bof-app-description-level";

export function loadDescriptionLevel(): DescriptionLevel {
  const value = readPref(DESCRIPTION_PREF_KEY);
  return value !== null && isDescriptionLevel(value) ? value : defaultDescriptionLevel;
}

export function saveDescriptionLevel(level: DescriptionLevel): void {
  writePref(DESCRIPTION_PREF_KEY, level);
}
