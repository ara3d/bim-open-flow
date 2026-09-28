// Persisted node-style choice (localStorage), validated against the style
// seam so a stale or foreign value falls back to the default. The
// themeChoice.ts pattern.

import { defaultNodeStyle, isNodeStyleName, type NodeStyleName } from "@bimopenflow/graph";
import { readPref, writePref } from "./prefs.js";

export const NODE_STYLE_PREF_KEY = "bof-app-node-style";

export function loadNodeStyleChoice(): NodeStyleName {
  const value = readPref(NODE_STYLE_PREF_KEY);
  return value !== null && isNodeStyleName(value) ? value : defaultNodeStyle;
}

export function saveNodeStyleChoice(name: NodeStyleName): void {
  writePref(NODE_STYLE_PREF_KEY, name);
}
