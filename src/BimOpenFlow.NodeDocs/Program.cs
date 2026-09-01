using BimOpenFlow.NodeDocs;
using BimOpenFlow.Nodes.Bos;
using BimOpenFlow.Nodes.Compliance;
using BimOpenFlow.Nodes.DuckDb;
using BimOpenFlow.Nodes.Effects;
using BimOpenFlow.Nodes.Geometry;
using BimOpenFlow.Nodes.Tables;

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
