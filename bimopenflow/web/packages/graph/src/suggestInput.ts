// Native-combobox behavior for a text input: a <datalist> filled lazily from
// a SuggestionList fetch on focus. Free text always stays allowed — the list
// is advisory. Unready/Unavailable put the reason in the input's tooltip.

import type { Suggestion, SuggestionList } from "@bimopenflow/contracts";
import { attachAggregateBuilder } from "./aggregateEditor.js";
import { columnList, completionOptions, toggleColumn } from "./suggestText.js";

export type SuggestFetch = () => Promise<SuggestionList>;

export interface SuggestOptions {
  /** Show every suggestion in a popup list on focus, whatever the box holds
   *  (a native datalist filters by the text already typed, so a field holding
   *  "Category" would list one column). `multi`: a pick toggles the column in
   *  or out of the comma-separated value; otherwise it replaces the value. */
  readonly picker?: { readonly multi: boolean };
  /** Edit a comma-separated `func(column) as name` list with a function, a column and a name
   *  per aggregate (aggregateEditor.ts), offering the functions each column's suggestion allows. */
  readonly aggregates?: boolean;
}

/**
 * Wires suggestions onto `input` via a document-level datalist with id
 * `listId`. Fetches once per focus; re-derives token-completed options as the
 * user types. Returns a cleanup that removes the datalist and listeners.
 */
export function attachSuggestions(
  input: HTMLInputElement,
  listId: string,
  fetch: SuggestFetch,
  options: SuggestOptions = {},
): () => void {
  if (options.aggregates) return attachAggregateBuilder(input, fetch);
  if (options.picker) return attachPicker(input, fetch, options.picker.multi);
  const doc = input.ownerDocument;
  const list = doc.createElement("datalist");
  list.id = listId;
  doc.body.appendChild(list);
  input.setAttribute("list", listId);

  let values: string[] = [];
  let fetchToken = 0;

  const render = () => {
    list.textContent = "";
    for (const option of completionOptions(input.value, values)) {
      const el = doc.createElement("option");
      el.value = option;
      list.appendChild(el);
    }
  };

  const onFocus = async () => {
    const token = ++fetchToken;
    try {
      const result = await fetch();
      if (token !== fetchToken) return; // a later focus superseded this fetch
      values = result.status === "Ok" ? result.values.map((v) => v.value) : [];
      input.title = result.status === "Ok" ? "" : (result.reason ?? "");
      render();
    } catch {
      values = [];
      render();
    }
  };
  const onInput = () => render();

  input.addEventListener("focus", onFocus);
  input.addEventListener("input", onInput);
  return () => {
    input.removeEventListener("focus", onFocus);
    input.removeEventListener("input", onInput);
    input.removeAttribute("list");
    list.remove();
  };
}

/** The popup list: one row per suggestion with its detail (the column type)
 *  beside it and, under the name, the column kind's meaning when it has one.
 *  Rows take mousedown so the input keeps focus; a pick sets the value and fires "change", the event the parameter row commits on. */
function attachPicker(input: HTMLInputElement, fetch: SuggestFetch, multi: boolean): () => void {
  const doc = input.ownerDocument;
  const popup = doc.createElement("div");
  popup.setAttribute("role", "listbox");
  popup.style.cssText =
    "position:fixed;z-index:10000;max-height:240px;overflow:auto;background:#fff;color:#222;" +
    "border:1px solid #aaa;border-radius:4px;font:13px system-ui;box-shadow:0 4px 12px #0004";
  popup.hidden = true;
  doc.body.appendChild(popup);

  let items: Suggestion[] = [];
  let fetchToken = 0;

  const hide = () => { fetchToken++; popup.hidden = true; };
  const render = () => {
    popup.textContent = "";
    const chosen = columnList(input.value);
    for (const item of items) {
      const row = doc.createElement("div");
      row.setAttribute("role", "option");
      const on = multi ? chosen.includes(item.value) : input.value === item.value;
      row.setAttribute("aria-selected", String(on));
      row.style.cssText = "display:flex;gap:12px;justify-content:space-between;padding:3px 8px;cursor:pointer;" +
        (on ? "font-weight:600;background:#e8f0fe" : "");
      const name = doc.createElement("span");
      name.textContent = (multi ? (on ? "✓ " : " ") : "") + item.value;
      const type = doc.createElement("span");
      type.textContent = item.detail ?? "";
      type.style.opacity = "0.6";
      row.append(name, type);
      if (item.meaning) {
        row.title = item.meaning;
        row.style.flexWrap = "wrap";
        const meaning = doc.createElement("span");
        meaning.textContent = item.meaning;
        meaning.style.cssText = "flex-basis:100%;font-size:11px;opacity:0.6;max-width:320px";
        row.appendChild(meaning);
      }
      row.addEventListener("mousedown", (e) => {
        e.preventDefault();
        input.value = multi ? toggleColumn(input.value, item.value) : item.value;
        input.dispatchEvent(new Event("change", { bubbles: true }));
        if (multi) render(); else hide();
      });
      popup.appendChild(row);
    }
  };
  const show = () => {
    if (items.length === 0) return hide();
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
      items = result.status === "Ok" ? result.values : [];
      input.title = result.status === "Ok" ? "" : (result.reason ?? "");
    } catch {
      items = [];
    }
    show();
  };
  const onInput = () => { if (!popup.hidden) render(); };
  const onKey = (e: KeyboardEvent) => { if (e.key === "Escape" || e.key === "Enter") hide(); };

  input.addEventListener("focus", onFocus);
  input.addEventListener("click", show);
  input.addEventListener("input", onInput);
  input.addEventListener("blur", hide);
  input.addEventListener("keydown", onKey);
  return () => {
    fetchToken++;
    input.removeEventListener("focus", onFocus);
    input.removeEventListener("click", show);
    input.removeEventListener("input", onInput);
    input.removeEventListener("blur", hide);
    input.removeEventListener("keydown", onKey);
    popup.remove();
  };
}
