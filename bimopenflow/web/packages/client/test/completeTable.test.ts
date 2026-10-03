import { describe, it, expect, vi } from "vitest";
import { completeTable } from "../src/completeTable";
const columns = [{name:"entityId",type:"Integer" as const}];
describe("complete spatial results", () => {
  it("includes rows beyond the default table page", async () => {
    const requestTable = vi.fn(async (_node:string,_port:string,skip=0) => ({
      columns, rows: skip === 0 ? [[1],[2]] : [[3]], skip, totalRows:3,
    }));
    const result = await completeTable({requestTable,resolveAsset:u=>u}, "n","instances",()=>true);
    expect(result?.rows).toEqual([[1],[2],[3]]);
    expect(requestTable.mock.calls.map(c=>c[2])).toEqual([0,2]);
  });
  it("stops fetching after the selected node changes", async () => {
    let current = true;
    const requestTable = vi.fn(async () => { current = false; return {columns,rows:[[1]],skip:0,totalRows:4}; });
    expect(await completeTable({requestTable,resolveAsset:u=>u},"n","instances",()=>current)).toBeUndefined();
    expect(requestTable).toHaveBeenCalledTimes(1);
  });
  it("rejects an empty page before the declared end", async () => {
    await expect(completeTable({requestTable:async()=>({columns,rows:[],skip:0,totalRows:4}),resolveAsset:u=>u},"n","instances",()=>true)).rejects.toThrow("incomplete");
  });
});
