import type { TableSlice } from "@bimopenflow/contracts";
import type { PaneContext } from "@bimopenflow/panes";

/** A spatial filter needs the whole result, not the table pane's first page. */
export async function completeTable(
  ctx: PaneContext, nodeId: string, port: string, isCurrent: () => boolean,
): Promise<TableSlice | undefined> {
  const pageSize = 10000;
  let result: TableSlice | undefined;
  let skip = 0;
  do {
    if (!isCurrent()) return undefined;
    const page = await ctx.requestTable(nodeId, port, skip, pageSize);
    if (!isCurrent()) return undefined;
    if (page.totalRows > 1000000) throw new Error("3D result exceeds one million rows. Use view3d.scene or reduce the instance table.");
    if (page.skip !== skip || (result && (result.totalRows !== page.totalRows ||
        JSON.stringify(result.columns) !== JSON.stringify(page.columns))))
      throw new Error("The 3D result changed while loading. Run the analysis again.");
    if (!result) result = { ...page, rows: [...page.rows], skip: 0 };
    else result.rows.push(...page.rows);
    skip += page.rows.length;
    if (skip > page.totalRows || (page.rows.length === 0 && skip < page.totalRows))
      throw new Error("The host returned an incomplete 3D result.");
  } while (skip < result.totalRows);
  return result;
}
