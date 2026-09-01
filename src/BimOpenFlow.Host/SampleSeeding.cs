using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host;

/// <summary>
/// Seeds an empty analysis store with the committed sample analyses
/// (samples/analyses/*.json), rewriting the {SAMPLES} path placeholder to the
/// absolute samples/tables directory. A non-empty store is never touched.
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
    public static IReadOnlyList<string> SeedIfEmpty(AnalysisStore store, string startDir)
        => FindRepoRoot(startDir) is { } root
            ? SeedIfEmpty(store,
                Path.Combine(root, "samples", "analyses"),
                Path.Combine(root, "samples", "tables"))
            : [];

    /// <summary>Seeds every analysesDir *.json (file stem = analysis id) into an
    /// empty store, pointing {SAMPLES} at samplesDir. Returns the seeded ids.</summary>
    public static IReadOnlyList<string> SeedIfEmpty(AnalysisStore store, string analysesDir, string samplesDir)
        => SeedIfEmpty(store, [(analysesDir, PathPlaceholder, samplesDir)]);

    /// <summary>Seeds every source's *.json (file stem = analysis id) into an
    /// empty store, replacing each source's placeholder with its target
    /// directory. Missing source dirs are skipped. Returns the seeded ids.</summary>
    public static IReadOnlyList<string> SeedIfEmpty(AnalysisStore store,
        IReadOnlyList<(string AnalysesDir, string Placeholder, string TargetDir)> sources)
        => store.List().Count > 0
            ? []
            : sources.SelectMany(s => Seed(store, s.AnalysesDir, s.Placeholder, s.TargetDir)).ToList();

    private static IEnumerable<string> Seed(AnalysisStore store, string analysesDir, string placeholder, string targetDir)
    {
        if (!Directory.Exists(analysesDir))
            yield break;
        foreach (var file in Directory.EnumerateFiles(analysesDir, "*.json").Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(file);
            store.Save(id, RewritePaths(GraphDocumentIO.Load(file), placeholder, targetDir));
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
