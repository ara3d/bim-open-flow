// Pure text helpers for suggestion comboboxes. Multi-value params (comma
// separated) complete the token at the end: each option is the text up to the
// last comma plus a suggested value, so the browser's datalist filtering
// matches what the user has typed so far.

/** Datalist option strings for `text` given the suggested values. */
export function completionOptions(text: string, values: readonly string[]): string[] {
  const idx = text.lastIndexOf(",");
  if (idx < 0) return [...values];
  const token = text.slice(idx + 1);
  const pad = token.slice(0, token.length - token.trimStart().length);
  const base = text.slice(0, idx + 1) + pad;
  return values.map((v) => base + v);
}

/** True when a column parameter holds a comma-separated list. The catalog says so (`list` on the
 *  parameter descriptor); the name is never consulted. A value that already has a comma is a list
 *  whatever the flag says, so the picker cannot collapse it to one name. */
export function isMultiColumn(value: string, list?: boolean): boolean {
  return list === true || value.includes(",");
}

/** The names in a comma-separated column list, trimmed, empty entries dropped. */
export function columnList(value: string): string[] {
  return value.split(",").map((s) => s.trim()).filter((s) => s !== "");
}

/** `value` with `column` removed when listed, appended when not. */
export function toggleColumn(value: string, column: string): string {
  const list = columnList(value);
  return (list.includes(column) ? list.filter((c) => c !== column) : [...list, column]).join(",");
}
