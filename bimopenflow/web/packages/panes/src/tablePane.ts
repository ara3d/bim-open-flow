import type { TableSlice } from "@bimopenflow/contracts";
import { DataTableView, type DataTableOptions } from "@bimopenflow/viz";
import type { Pane } from "./pane";
import { definePane } from "./base";
import { idColumnIndex } from "./columns";

export type TablePaneOptions = DataTableOptions;

/**
 * Table pane: wraps the viz DataTableView.
 *
 * Inputs: "table" renders/updates the table; "selection" highlights rows
 * whose id-cell text matches one of the ids. Emits "selection" on row click,
 * with the id read from the id column — "globalId" if present, else
 * "entityId", else the first column (rendered cell text, so Integer ids
 * become their plain decimal string).
 */
export const createTablePane = (options?: TablePaneOptions): Pane =>
  definePane((root, _ctx, emit) => {
    let handle: { update(data: TableSlice): void; destroy(): void } | null =
      null;
    let idIdx = 0;
    let selected: ReadonlySet<string> = new Set();

    const highlight = (): void => {
      for (const tr of root.querySelectorAll("tbody tr")) {
        const cell = tr.children[idIdx];
        const id = cell?.textContent ?? "";
        tr.classList.toggle("bof-panes-selected", selected.has(id));
      }
    };

    // The viz table re-renders itself on header-click sorting; reapply the
    // highlight whenever the DOM under the pane changes.
    const observer = new MutationObserver(highlight);
    observer.observe(root, { childList: true, subtree: true });

    root.addEventListener("click", (e) => {
      const tr = (e.target as Element | null)?.closest("tbody tr");
      if (!tr) return;
      const id = tr.children[idIdx]?.textContent ?? "";
      selected = new Set([id]);
      highlight();
      emit({ kind: "selection", event: { source: "table", ids: [id] } });
    });

    return {
      update(input) {
        if (input.kind === "table") {
          idIdx = idColumnIndex(input.data.columns);
          if (handle) handle.update(input.data);
          else handle = DataTableView.mount(root, input.data, options);
          highlight();
        } else if (input.kind === "selection") {
          selected = new Set(input.ids);
          highlight();
        }
      },
      destroy() {
        observer.disconnect();
        handle?.destroy();
      },
    };
  });
