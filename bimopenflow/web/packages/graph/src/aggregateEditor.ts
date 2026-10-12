// The aggregate builder: edits a comma-separated list of `func(column) as name` with a function,
// a column and a name per aggregate, in a popup under the text input. The input stays the source of
// truth and stays editable; every change rewrites its value and fires "change", the event the
// parameter row commits on. Which functions a column allows arrives in its suggestion
// (`functions`, from the host's aggregate rule), so this file holds no table of types.

import type { Suggestion } from "@bimopenflow/contracts";
import { columnList } from "./suggestText.js";

export interface Aggregate {
  readonly func: string;
  /** "*" for the whole row. */
  readonly column: string;
  readonly name: string;
}

const SPEC = /^(\w+)\s*\(\s*(\*|[^)]*?)\s*\)\s+as\s+(.+)$/i;

/** The aggregates in `value`; an entry that does not read as `func(column) as name` comes back
 *  as `raw` so editing the others never drops it. */
export function parseAggregates(value: string): (Aggregate | { readonly raw: string })[] {
  return columnList(value).map((entry) => {
    const m = SPEC.exec(entry);
    return m ? { func: m[1]!.toLowerCase(), column: m[2]!, name: m[3]!.trim() } : { raw: entry };
  });
}

export function formatAggregate(a: Aggregate): string {
  return `${a.func}(${a.column}) as ${a.name}`;
}

/** The name a new aggregate gets: `sum_Volume`, or `count` for the whole row. */
export function defaultName(func: string, column: string): string {
  return column === "*" ? func : `${func}_${column}`;
}

type Entry = Aggregate | { readonly raw: string };
const isAggregate = (e: Entry): e is Aggregate => "func" in e;

export function attachAggregateBuilder(input: HTMLInputElement, fetch: () => Promise<{ status: string; reason?: string | undefined; values: Suggestion[] }>): () => void {
  const doc = input.ownerDocument;
  const popup = doc.createElement("div");
  popup.setAttribute("role", "dialog");
  popup.setAttribute("aria-label", "Aggregates");
  popup.style.cssText =
    "position:fixed;z-index:10000;padding:6px;display:flex;flex-direction:column;gap:4px;background:#fff;color:#222;" +
    "border:1px solid #aaa;border-radius:4px;font:13px system-ui;box-shadow:0 4px 12px #0004";
  popup.hidden = true;
  doc.body.appendChild(popup);

  let columns: Suggestion[] = [];
  let fetchToken = 0;

  const functionsOf = (column: string, current: string): string[] =>
    columns.find((c) => c.value === column)?.functions?.slice() ?? [current];
  const write = (entries: Entry[]) => {
    input.value = entries.map((e) => (isAggregate(e) ? formatAggregate(e) : e.raw)).join(",");
    input.dispatchEvent(new Event("change", { bubbles: true }));
    render();
  };
  const hide = () => { fetchToken++; popup.hidden = true; };

  const select = (label: string, options: readonly { value: string; text: string }[], value: string) => {
    const el = doc.createElement("select");
    el.setAttribute("aria-label", label);
    for (const o of options) el.appendChild(new Option(o.text, o.value));
    if (!options.some((o) => o.value === value)) el.appendChild(new Option(value, value));
    el.value = value;
    return el;
  };
  const button = (label: string, text: string, onClick: () => void) => {
    const el = doc.createElement("button");
    el.type = "button";
    el.setAttribute("aria-label", label);
    el.textContent = text;
    el.addEventListener("click", onClick);
    return el;
  };

  const rowFor = (entries: Entry[], index: number): HTMLElement => {
    const row = doc.createElement("div");
    row.style.cssText = "display:flex;gap:4px;align-items:center";
    const entry = entries[index]!;
    const replace = (next: Entry | null) => write(entries.flatMap((e, i) => (i !== index ? [e] : next ? [next] : [])));
    if (!isAggregate(entry)) {
      const raw = doc.createElement("span");
      raw.textContent = entry.raw;
      raw.title = "Not in the form func(column) as name; kept as written";
      row.append(raw, button(`Remove ${entry.raw}`, "×", () => replace(null)));
      return row;
    }
    const func = select("Function", functionsOf(entry.column, entry.func).map((f) => ({ value: f, text: f })), entry.func);
    const column = select("Column", columns.map((c) => ({ value: c.value, text: c.value === "*" ? "* (rows)" : `${c.value}  ${c.detail ?? ""}` })), entry.column);
    const name = doc.createElement("input");
    name.setAttribute("aria-label", "Name");
    name.value = entry.name;
    name.style.width = "110px";
    func.addEventListener("change", () => replace({ ...entry, func: func.value, name: entry.name === defaultName(entry.func, entry.column) ? defaultName(func.value, entry.column) : entry.name }));
    column.addEventListener("change", () => {
      const allowed = functionsOf(column.value, entry.func);
      const next = allowed.includes(entry.func) ? entry.func : allowed[0]!;
      replace({ func: next, column: column.value,
        name: entry.name === defaultName(entry.func, entry.column) ? defaultName(next, column.value) : entry.name });
    });
    name.addEventListener("change", () => replace(name.value.trim() === "" ? entry : { ...entry, name: name.value.trim() }));
    row.append(func, column, name, button(`Remove ${entry.name}`, "×", () => replace(null)));
    return row;
  };

  function render(): void {
    popup.textContent = "";
    const entries = parseAggregates(input.value);
    entries.forEach((_, i) => popup.appendChild(rowFor(entries, i)));
    const first = columns[0];
    if (first)
      popup.appendChild(button("Add aggregate", "+ Add", () => {
        const func = first.functions?.[0] ?? "count";
        write([...entries, { func, column: first.value, name: defaultName(func, first.value) }]);
      }));
  }
  const show = () => {
    render();
    const r = input.getBoundingClientRect();
    popup.style.left = `${r.left}px`;
    popup.style.top = `${r.bottom}px`;
    popup.style.minWidth = `${r.width}px`;
    popup.hidden = false;
  };

  const onFocus = async () => {
    const token = ++fetchToken;
    try {
      const result = await fetch();
      if (token !== fetchToken) return;
      columns = result.status === "Ok" ? result.values : [];
      input.title = result.status === "Ok" ? "" : (result.reason ?? "");
    } catch {
      columns = [];
    }
    show();
  };
  // Focus moving into the popup (a select, the name box) must not close it.
  const onFocusOut = () => setTimeout(() => {
    const active = doc.activeElement;
    if (active !== input && !popup.contains(active)) hide();
  }, 0);
  const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") hide(); };

  input.addEventListener("focus", onFocus);
  input.addEventListener("focusout", onFocusOut);
  popup.addEventListener("focusout", onFocusOut);
  input.addEventListener("keydown", onKey);
  popup.addEventListener("keydown", onKey);
  return () => {
    fetchToken++;
    input.removeEventListener("focus", onFocus);
    input.removeEventListener("focusout", onFocusOut);
    input.removeEventListener("keydown", onKey);
    popup.remove();
  };
}
