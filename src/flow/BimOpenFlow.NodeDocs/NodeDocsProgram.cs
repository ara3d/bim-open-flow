using BimOpenFlow.Nodes.Cleaning;
using BimOpenFlow.Nodes.Compliance;
using BimOpenFlow.Nodes.Dates;
using BimOpenFlow.Nodes.DuckDb;
using BimOpenFlow.Nodes.Effects;
using BimOpenFlow.Nodes.Relations;
using BimOpenFlow.Nodes.Spatial;
using BimOpenFlow.Nodes.TableOps;
using BimOpenFlow.Nodes.Tables;
using BimOpenFlow.Nodes.Viz;

namespace BimOpenFlow.NodeDocs;

/// <summary>Writes the node reference for a list of packs. This project names only the
/// generic packs and their notes; a front end that composes more (the studio's BIM packs)
/// puts its own packs in front of them, adds its notes, and calls Run.</summary>
public static class NodeDocsProgram
{
    /// <summary>The packs that need no BIM Open Schema, in the order the reference lists them.</summary>
    public static readonly IReadOnlyList<Pack> GenericPacks =
    [
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
        new("Spatial — `BimOpenFlow.Nodes.Spatial`",
            "GIS-style predicates and measures (intersects, within, nearest, contains, footprints, polygons) over "
            + "point, box, and WKT polygon columns; every join emits a pairs table. BIM-free and DuckDB-free.",
            SpatialNodes.All),
        new("Relations — `BimOpenFlow.Nodes.Relations`",
            "The rel.* pack: wires carry a logical plan plus its schema instead of rows. A chain compiles to one "
            + "SQL statement and runs only when inspected or materialized. Sources are named through the host's "
            + "connection registry (each model root folder, and each .duckdb file inside one).",
            RelationNodes.All(RelationRuntime.FromRoots([]))),
    ];

    /// <summary>Writes the reference for the packs, with the notes keyed by node kind, to
    /// outputPath and says where.</summary>
    public static int Run(string outputPath, IReadOnlyList<Pack> packs, IReadOnlyDictionary<string, string> notes)
    {
        File.WriteAllText(outputPath, MarkdownEmitter.Render(packs, notes));
        Console.WriteLine($"Wrote {outputPath}");
        return 0;
    }
}
