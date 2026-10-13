// Persisted card density (localStorage, TKT-296), validated against the graph
// package's density seam so a stale or foreign value falls back to the
// default. One choice per browser, shared by every look: the
// descriptionChoice.ts pattern.

import { defaultCardDensity, isCardDensity, type CardDensity } from "@bimopenflow/graph";
import { readPref, writePref } from "./prefs.js";

export const CARD_DENSITY_PREF_KEY = "bof-app-card-density";

export function loadCardDensity(): CardDensity {
  const value = readPref(CARD_DENSITY_PREF_KEY);
  return value !== null && isCardDensity(value) ? value : defaultCardDensity;
}

export function saveCardDensity(density: CardDensity): void {
  writePref(CARD_DENSITY_PREF_KEY, density);
}
