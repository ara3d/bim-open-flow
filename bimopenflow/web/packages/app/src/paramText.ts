// Canonical-string conversions for parameter editing. The graph document
// stores every value in canonical string form (spec format.md §4); editors
// parse and format at the edge, so everything here is pure text-in/text-out.

/** "123" -> "123"; anything that is not a whole Int64-ish number -> null. */
export function normalizeInteger(text: string): string | null {
  const t = text.trim();
  return /^-?\d+$/.test(t) ? String(BigInt(t)) : null;
}

/** Any finite JS number text -> its round-trip form; otherwise null. */
export function normalizeNumber(text: string): string | null {
  const t = text.trim();
  if (t === "" || !/^[-+0-9.eE]+$/.test(t)) return null;
  const n = Number(t);
  return Number.isFinite(n) ? String(n) : null;
}

/**
 * Canonical DateTime ("yyyy-MM-dd" or "yyyy-MM-ddTHH:mm:ss", empty = unset)
 * -> the value a <input type="datetime-local"> accepts ("yyyy-MM-ddTHH:mm").
 */
export function toDatetimeLocal(canonical: string): string {
  if (canonical === "") return "";
  if (/^\d{4}-\d{2}-\d{2}$/.test(canonical)) return `${canonical}T00:00`;
  const m = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})(:\d{2})?$/.exec(canonical);
  return m ? m[1]! : "";
}

/** <input type="datetime-local"> value -> canonical DateTime form. */
export function fromDatetimeLocal(value: string): string {
  if (value === "") return "";
  if (/^\d{4}-\d{2}-\d{2}T00:00(:00)?$/.test(value)) return value.slice(0, 10);
  const m = /^(\d{4}-\d{2}-\d{2}T\d{2}:\d{2})(:\d{2})?$/.exec(value);
  return m ? `${m[1]}${m[2] ?? ":00"}` : "";
}

/** A FilePath canonical value's last path segment (either separator); the
 *  whole value, unchanged, when it holds no separator. Used to keep a node
 *  card readable — "duplex-enriched.ifc" instead of the machine-local
 *  "C:/Users/.../samples/duplex-enriched.ifc" prefix — while the full path
 *  stays the value edited and the title shown on hover. */
export function fileName(path: string): string {
  const cut = Math.max(path.lastIndexOf("/"), path.lastIndexOf("\\"));
  return cut === -1 ? path : path.slice(cut + 1);
}
