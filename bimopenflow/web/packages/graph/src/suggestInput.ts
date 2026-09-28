// Native-combobox behavior for a text input: a <datalist> filled lazily from
// a SuggestionList fetch on focus. Free text always stays allowed — the list
// is advisory. Unready/Unavailable put the reason in the input's tooltip.

import type { SuggestionList } from "@bimopenflow/contracts";
import { completionOptions } from "./suggestText.js";

export type SuggestFetch = () => Promise<SuggestionList>;

/**
 * Wires suggestions onto `input` via a document-level datalist with id
 * `listId`. Fetches once per focus; re-derives token-completed options as the
 * user types. Returns a cleanup that removes the datalist and listeners.
 */
export function attachSuggestions(
  input: HTMLInputElement,
  listId: string,
  fetch: SuggestFetch,
): () => void {
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
