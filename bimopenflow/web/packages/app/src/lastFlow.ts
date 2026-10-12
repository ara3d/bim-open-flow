// The flow last opened in the editor, kept in localStorage so the root URL
// reopens it (TKT-254). One key for every editor page on an origin: they
// share one host store, so the id means the same flow on each.
import { readPref, writePref } from "./prefs.js";

export const LAST_FLOW_KEY = "bof-app-last-flow";

export const loadLastFlow = (): string | undefined => readPref(LAST_FLOW_KEY) || undefined;

export const saveLastFlow = (id: string): void => writePref(LAST_FLOW_KEY, id);
