import type { ColumnType, TableData } from "@bimopenflow/contracts";
import { defineComponent } from "./component";
import { formatValue, isNumericType, numberOf } from "./format";
import { columnIndexByName } from "./columns";

export interface DataTableOptions {
  /** Maximum rows rendered; a "showing N of M rows" footer appears beyond it. */
  maxRows?: number;
  /** Column-header click sorting. Default true. */
  sortable?: boolean;
}

const DEFAULT_MAX_ROWS = 1000;

interface SortState {
  name: string;
  descending: boolean;
}

const compareBy = (type: ColumnType): ((a: unknown, b: unknown) => number) =>
  isNumericType(type)
    ? (a, b) => numberOf(a) - numberOf(b)
    : type === "Boolean"
      ? (a, b) => Number(Boolean(a)) - Number(Boolean(b))
      : (a, b) => {
          const sa = String(a);
          const sb = String(b);
          return sa < sb ? -1 : sa > sb ? 1 : 0;
        };

/** Sorted copy: nulls always last, direction applied to the rest. */
const sortRows = (
  rows: readonly unknown[][],
  column: number,
  type: ColumnType,
  descending: boolean,
): unknown[][] => {
  const cmp = compareBy(type);
  const dir = descending ? -1 : 1;
  return [...rows].sort((a, b) => {
    const av = a[column];
    const bv = b[column];
    const an = av === null || av === undefined;
    const bn = bv === null || bv === undefined;
    if (an || bn) return an && bn ? 0 : an ? 1 : -1;
    return dir * cmp(av, bv);
  });
};

export const DataTableView = defineComponent<TableData, DataTableOptions>(
  (root, options) => {
    const maxRows = options?.maxRows ?? DEFAULT_MAX_ROWS;
    const sortable = options?.sortable ?? true;
    let current: TableData;
    let sort: SortState | undefined;

    const render = () => {
      const doc = root.ownerDocument;
      root.textContent = "";
      const table = doc.createElement("table");
      table.className = "bof-viz-table";

      const sortIndex = sort ? columnIndexByName(current, sort.name) : -1;

      const thead = doc.createElement("thead");
      const headRow = doc.createElement("tr");
      current.columns.forEach((col, i) => {
        const th = doc.createElement("th");
        const marker =
          i === sortIndex ? (sort!.descending ? " ▼" : " ▲") : "";
        th.textContent = col.name + marker;
        th.dataset.column = col.name;
        if (isNumericType(col.type)) th.classList.add("bof-viz-num");
        if (sortable)
          th.addEventListener("click", () => {
            sort =
              sort?.name === col.name
                ? { name: col.name, descending: !sort.descending }
                : { name: col.name, descending: false };
            render();
          });
        headRow.appendChild(th);
      });
      thead.appendChild(headRow);
      table.appendChild(thead);

      const rows =
        sortIndex >= 0
          ? sortRows(
              current.rows,
              sortIndex,
              current.columns[sortIndex].type,
              sort!.descending,
            )
          : current.rows;
      const shown = rows.slice(0, maxRows);

      const tbody = doc.createElement("tbody");
      for (const row of shown) {
        const tr = doc.createElement("tr");
        current.columns.forEach((col, i) => {
          const td = doc.createElement("td");
          td.textContent = formatValue(row[i], col.type);
          if (isNumericType(col.type)) td.classList.add("bof-viz-num");
          tr.appendChild(td);
        });
        tbody.appendChild(tr);
      }
      table.appendChild(tbody);
      root.appendChild(table);

      if (rows.length > shown.length) {
        const footer = doc.createElement("div");
        footer.className = "bof-viz-footer";
        footer.textContent = `showing ${shown.length} of ${rows.length} rows`;
        root.appendChild(footer);
      }
    };

    return (data) => {
      current = data;
      render();
    };
  },
);
