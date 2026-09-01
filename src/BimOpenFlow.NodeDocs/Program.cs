using BimOpenFlow.NodeDocs;
using BimOpenFlow.Nodes.Bos;
using BimOpenFlow.Nodes.Cleaning;
using BimOpenFlow.Nodes.Compliance;
using BimOpenFlow.Nodes.Dates;
using BimOpenFlow.Nodes.DuckDb;
using BimOpenFlow.Nodes.Effects;
using BimOpenFlow.Nodes.Geometry;
using BimOpenFlow.Nodes.TableOps;
using BimOpenFlow.Nodes.Tables;
using BimOpenFlow.Nodes.Viz;

var packs = new Pack[]
{
    new("BOS — `BimOpenFlow.Nodes.Bos`",
        "Loading BIM Open Schema (.bos) files and the core table transforms: filter, derive, aggregate, sort.",
        BosNodes.All),
    new("Geometry — `BimOpenFlow.Nodes.Geometry`",
        "The view3d pack: the tables the 3D pane consumes — instances, colors, isolation, camera.",
        GeometryNodes.All),
    new("Compliance — `BimOpenFlow.Nodes.Compliance`",
        "The verdict-bearing vocabulary: rule checks, required-data checks, rollups, and unions of verdict tables.",
        ComplianceNodes.All),
    new("Effects — `BimOpenFlow.Nodes.Effects`",
        "Every Run-gated sink: CSV export, IFC property-set write-back, and HTML reports.",
        EffectNodes.All),
    new("DuckDB — `BimOpenFlow.Nodes.DuckDb`",
        "File readers backed by DuckDB and SQL over flowing tables. BIM-free; every value is a plain table.",
        DuckDbNodes.All),
    new("Tables — `BimOpenFlow.Nodes.Tables`",
        "XLSX and SQLite readers plus table combinators: join, set operations, and projection. BIM-free, DuckDB-free.",
        TableNodes.All),
    new("TableOps — `BimOpenFlow.Nodes.TableOps`",
        "Rows, columns, reshape, and window transforms — each a typed facade over one generated DuckDB clause.",
        TableOpsNodes.All),
    new("Cleaning — `BimOpenFlow.Nodes.Cleaning`",
        "Nulls, duplicates, text noise, and value replacement: the messy-data fixes that run before shaping.",
        CleaningNodes.All),
    new("Dates — `BimOpenFlow.Nodes.Dates`",
        "Parsing text columns into dates, extracting parts, truncating, arithmetic, and range filtering.",
        DatesNodes.All),
    new("Viz — `BimOpenFlow.Nodes.Viz`",
        "Chart and table-view nodes that validate and project table data for the web panes; rendering stays client-side.",
        VizNodes.All),
};

var outputPath = args.Length > 0 ? args[0] : Path.Combine(FindRepoRoot(), "docs", "nodes.md");
File.WriteAllText(outputPath, MarkdownEmitter.Render(packs));
Console.WriteLine($"Wrote {outputPath}");
return 0;

static string FindRepoRoot()
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "BimOpenToolkit.sln")))
            return dir.FullName;
    throw new InvalidOperationException(
        "BimOpenToolkit.sln not found above the executable; pass the output path as the first argument.");
}
