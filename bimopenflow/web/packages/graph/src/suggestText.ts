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
