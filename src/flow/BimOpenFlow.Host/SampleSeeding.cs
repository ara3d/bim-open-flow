using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host;

/// <summary>
/// Seeds an empty analysis store with the committed sample analyses
/// (samples/analyses/*.json, rewriting the {SAMPLES} path placeholder to the
/// absolute samples/tables directory; samples/relations/*.json and
/// samples/nrc-analyses/*.json, which name their sources; and
/// samples/showcase-analyses/*.json over samples/nrc). A non-empty store is never
/// touched. Given the profile's registry, a graph whose node kinds the profile does
/// not carry is skipped and reported, so every seeded graph can be evaluated.
/// </summary>
public static class SampleSeeding
{
    public const string PathPlaceholder = "{SAMPLES}";
    public const string SolutionFileName = "BimOpenToolkit.sln";

    /// <summary>
    /// Seeds from the repo's samples directories, located by walking up from
    /// startDir to the solution file. Skips silently (returns empty) when the
    /// repo root is not found (installed deployments) or the store has content.
    /// Returns the seeded analysis ids in seed order.
    /// </summary>
    public static IReadOnlyList<string> SeedIfEmpty(AnalysisStore store, string startDir,
        INodeRegistry? registry = null, TextWriter? log = null)
        => FindRepoRoot(startDir) is { } root
            ? SeedIfEmpty(store,
            [
                (Path.Combine(root, "samples", "analyses"), PathPlaceholder, Path.Combine(root, "samples", "tables")),
                (Path.Combine(root, "samples", "relations"), PathPlaceholder, Path.Combine(root, "samples", "tables")),
                NrcAnalyses(root),
                ShowcaseAnalyses(root),
            ], registry, log)
            : [];

    /// <summary>The sample data directories the relation samples name as sources:
    /// "tables" (the folder) and "sample" (sample.duckdb), and "nrc" (the folder) and
    /// "duplex-enriched" (its database, prepared in the background by SamplePreparation),
    /// so the tables host can add them to its model roots. Empty outside a repo checkout.</summary>
    public static IReadOnlyList<string> SeededModelRoots(string startDir)
        => FindRepoRoot(startDir) is { } root ? [Path.Combine(root, "samples", "tables"), NrcSamplesDir(root)] : [];

    /// <summary>samples/nrc: the NRC paper's CSVs and IFC, named as sources "nrc" and "duplex-enriched".</summary>
    public static string NrcSamplesDir(string root)
        => Path.Combine(root, "samples", "nrc");

    public const string NrcIfcFileName = "duplex-enriched.ifc";
    public const string NrcDatabaseFileName = "duplex-enriched.duckdb";
    public const string NrcBosFileName = "duplex-enriched.bos";

    /// <summary>The seeding source for samples/nrc-analyses, whose graphs name their sources
    /// and so need no placeholder. Seeded by both host profiles.</summary>
    public static (string AnalysesDir, string Placeholder, string TargetDir) NrcAnalyses(string root)
        => (Path.Combine(root, "samples", "nrc-analyses"), PathPlaceholder, NrcSamplesDir(root));

    /// <summary>The seeding source for samples/showcase-analyses: the end-to-end demos over
    /// samples/nrc (CSV, the IFC, and the BOS and DuckDB prepared from it). Both profiles seed
    /// the graphs their registry can run.</summary>
    public static (string AnalysesDir, string Placeholder, string TargetDir) ShowcaseAnalyses(string root)
        => (Path.Combine(root, "samples", "showcase-analyses"), PathPlaceholder, NrcSamplesDir(root));

    /// <summary>Seeds every analysesDir *.json (file stem = analysis id) into an
    /// empty store, pointing {SAMPLES} at samplesDir. Returns the seeded ids.</summary>
    public static IReadOnlyList<string> SeedIfEmpty(AnalysisStore store, string analysesDir, string samplesDir)
        => SeedIfEmpty(store, [(analysesDir, PathPlaceholder, samplesDir)]);

    /// <summary>Seeds every source's *.json (file stem = analysis id) into an
    /// empty store, replacing each source's placeholder with its target
    /// directory. Missing source dirs are skipped. With a registry, a graph that
    /// does not validate against it (a node kind the profile lacks) is skipped and
    /// written to the log, so the store never holds a graph the host cannot run.
    /// Returns the seeded ids.</summary>
    public static IReadOnlyList<string> SeedIfEmpty(AnalysisStore store,
        IReadOnlyList<(string AnalysesDir, string Placeholder, string TargetDir)> sources,
        INodeRegistry? registry = null, TextWriter? log = null)
        => store.List().Count > 0
            ? []
            : sources.SelectMany(s => Seed(store, s.AnalysesDir, s.Placeholder, s.TargetDir, registry, log ?? TextWriter.Null)).ToList();

    private static IEnumerable<string> Seed(AnalysisStore store, string analysesDir, string placeholder, string targetDir,
        INodeRegistry? registry, TextWriter log)
    {
        if (!Directory.Exists(analysesDir))
            yield break;
        foreach (var file in Directory.EnumerateFiles(analysesDir, "*.json").Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var doc = RewritePaths(GraphDocumentIO.Load(file), placeholder, targetDir);
            var errors = registry is null ? [] : doc.Validate(registry);
            if (errors.Count > 0)
            {
                log.WriteLine($"  skipped sample analysis {id}: {string.Join("; ", errors.Select(e => e.Message).Distinct())}");
                continue;
            }
            store.Save(id, doc);
            yield return id;
        }
    }

    /// <summary>A copy of the document with {SAMPLES} in every parameter value
    /// replaced by the given directory (forward slashes, no trailing slash).</summary>
    public static GraphDocument RewritePaths(GraphDocument doc, string samplesDir)
        => RewritePaths(doc, PathPlaceholder, samplesDir);

    /// <summary>A copy of the document with the placeholder in every parameter
    /// value replaced by the given directory (forward slashes, no trailing slash).</summary>
    public static GraphDocument RewritePaths(GraphDocument doc, string placeholder, string targetDir)
    {
        var target = Path.GetFullPath(targetDir).Replace('\\', '/').TrimEnd('/');
        return doc with
        {
            Values = doc.Values.ToDictionary(
                node => node.Key,
                node => (IReadOnlyDictionary<string, string>)node.Value.ToDictionary(
                    p => p.Key,
                    p => p.Value.Replace(placeholder, target))),
        };
    }

    /// <summary>The nearest ancestor of startDir containing the solution file, or null.</summary>
    public static string? FindRepoRoot(string startDir)
    {
        for (var dir = new DirectoryInfo(startDir); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
                return dir.FullName;
        return null;
    }
}
