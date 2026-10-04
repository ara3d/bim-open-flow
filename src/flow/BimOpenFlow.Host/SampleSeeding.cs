using System.Text.RegularExpressions;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host;

/// <summary>
/// Seeds an analysis store with committed sample analyses, rewriting a path placeholder
/// such as {SAMPLES} in each graph to an absolute directory, and keeps the store's untouched
/// copies current when a sample changes (see Seed). The tables profile's samples are
/// samples/analyses/*.json and samples/relations/*.json over samples/tables; a host that
/// composes other profiles passes its own sources. Given the profile's registry, a graph whose
/// node kinds the profile does not carry is skipped and reported, so every seeded graph can
/// be evaluated.
/// </summary>
public static class SampleSeeding
{
    public const string PathPlaceholder = "{SAMPLES}";

    /// <summary>The checkout root is the nearest folder holding a solution file.</summary>
    public const string SolutionPattern = "*.sln";

    /// <summary>The tables profile's seeding: TableSources from the checkout containing
    /// startDir. Returns the ids seeded for the first time, in seed order.</summary>
    public static IReadOnlyList<string> Seed(AnalysisStore store, string startDir,
        INodeRegistry? registry = null, TextWriter? log = null)
        => SeedFromCheckout(store, startDir, TableSources, registry, log);

    /// <summary>Seeds from the sources the checkout containing startDir names, by the rules of
    /// the sources overload. Skips silently (returns empty) when no checkout is found
    /// (installed deployments).</summary>
    public static IReadOnlyList<string> SeedFromCheckout(AnalysisStore store, string startDir,
        Func<string, IReadOnlyList<(string AnalysesDir, string Placeholder, string TargetDir)>> sources,
        INodeRegistry? registry = null, TextWriter? log = null)
        => FindRepoRoot(startDir) is { } root ? Seed(store, sources(root), registry, log) : [];

    /// <summary>samples/analyses and samples/relations, both with {SAMPLES} pointed at samples/tables.</summary>
    public static IReadOnlyList<(string AnalysesDir, string Placeholder, string TargetDir)> TableSources(string root)
        =>
        [
            (Path.Combine(root, "samples", "analyses"), PathPlaceholder, TablesDir(root)),
            (Path.Combine(root, "samples", "relations"), PathPlaceholder, TablesDir(root)),
        ];

    /// <summary>samples/tables: the folder the relation samples name as source "tables", holding
    /// sample.duckdb (source "sample").</summary>
    public static string TablesDir(string root)
        => Path.Combine(root, "samples", "tables");

    /// <summary>The sample data directories the tables profile adds to its model roots:
    /// samples/tables. Empty outside a checkout.</summary>
    public static IReadOnlyList<string> SeededModelRoots(string startDir)
        => FindRepoRoot(startDir) is { } root ? [TablesDir(root)] : [];

    /// <summary>Seeds every analysesDir *.json (file stem = analysis id), pointing {SAMPLES}
    /// at samplesDir. Returns the ids seeded for the first time.</summary>
    public static IReadOnlyList<string> Seed(AnalysisStore store, string analysesDir, string samplesDir)
        => Seed(store, [(analysesDir, PathPlaceholder, samplesDir)]);

    /// <summary>
    /// Brings the store's copies of the sample analyses up to date with samples/, one
    /// analysis at a time, recording in SampleSeedRecord what each copy was seeded from:
    /// <list type="bullet">
    /// <item>a sample the store lacks is seeded, unless the user deleted it and the sample
    /// has not changed since;</item>
    /// <item>a copy still identical to what was seeded is refreshed when the sample changed
    /// (the store archives the old copy under versions/);</item>
    /// <item>a copy the user edited is kept, and the log says the sample changed.</item>
    /// </list>
    /// A store follows a sample folder when it was empty, when the record names the folder,
    /// or when it already holds one of the folder's analyses; other folders are left out, so
    /// a store prepared for one purpose (the DuckDB studio's) does not fill with another's
    /// samples. A store seeded before the record existed has no entries: a copy with no
    /// saved versions was never edited (every store save after the first archives one) and
    /// is refreshed; a copy with versions is kept; an analysis in .trash stays deleted.
    /// Each source's placeholder becomes its target directory; a graph with a placeholder
    /// left over, or (given a registry) one the profile cannot run, is skipped and logged.
    /// Returns the ids seeded for the first time; refreshed and kept copies are logged.
    /// </summary>
    public static IReadOnlyList<string> Seed(AnalysisStore store,
        IReadOnlyList<(string AnalysesDir, string Placeholder, string TargetDir)> sources,
        INodeRegistry? registry = null, TextWriter? log = null)
    {
        log ??= TextWriter.Null;
        var record = SampleSeedRecord.Read(store);
        var before = new Dictionary<string, SampleSeed>(record);
        var storeWasEmpty = store.List().Count == 0;
        var seeded = new List<string>();
        foreach (var (analysesDir, placeholder, targetDir) in sources)
        {
            if (!Directory.Exists(analysesDir))
                continue;
            var source = Path.GetFileName(Path.TrimEndingDirectorySeparator(analysesDir));
            var files = Directory.EnumerateFiles(analysesDir, "*.json").Order(StringComparer.Ordinal).ToList();
            var follows = storeWasEmpty
                || record.Values.Any(r => r.Source == source)
                || files.Select(f => Path.GetFileNameWithoutExtension(f)).Any(id => store.Exists(id) || IsInTrash(store, id));
            if (!follows)
                continue;
            foreach (var (id, doc) in Samples(files, placeholder, targetDir, registry, log))
                if (SeedOne(store, record, source, id, doc, log))
                    seeded.Add(id);
        }
        if (record.Count != before.Count || record.Any(e => !Equals(before.GetValueOrDefault(e.Key), e.Value)))
            SampleSeedRecord.Write(store, record);
        return seeded;
    }

    /// <summary>Applies the rules of Seed to one sample; true when it was seeded for the first time.</summary>
    private static bool SeedOne(AnalysisStore store, Dictionary<string, SampleSeed> record, string source,
        string id, GraphDocument doc, TextWriter log)
    {
        var hash = SampleSeedRecord.Hash(doc);
        var entry = record.GetValueOrDefault(id);
        var seed = new SampleSeed(source, hash);
        if (!store.Exists(id))
        {
            var deleted = entry is null ? IsInTrash(store, id) : entry.Sha256 == hash;
            if (deleted)
                return false;
            store.Save(id, doc);
            record[id] = seed;
            return true;
        }
        var current = SampleSeedRecord.Hash(store.Load(id));
        if (current == hash)
            record[id] = seed;
        else if (entry is null ? !HasHistory(store, id) : current == entry.Sha256)
        {
            store.Save(id, doc);
            record[id] = seed;
            log.WriteLine($"  refreshed sample analysis {id}: the sample changed; the previous copy is in its versions/");
        }
        else
        {
            // An empty hash stands for "seeded from a version nobody recorded", so deleting
            // the kept copy lets the next start seed the new sample.
            record[id] = entry ?? new SampleSeed(source, "");
            log.WriteLine($"  kept your edited copy of sample analysis {id}: samples/{source}/{id}.json has changed since. "
                + "To take the new sample, delete the analysis (it moves to .trash) and restart the host.");
        }
        return false;
    }

    private static bool IsInTrash(AnalysisStore store, string id)
        => Directory.Exists(Path.Combine(store.RootDir, AnalysisStore.TrashDirName, id));

    /// <summary>True when the store archived at least one earlier version of the analysis,
    /// which it does on every save after the first.</summary>
    private static bool HasHistory(AnalysisStore store, string id)
    {
        var versions = Path.Combine(store.RootDir, id, AnalysisStore.VersionsDirName);
        return Directory.Exists(versions) && Directory.EnumerateFiles(versions).Any();
    }

    /// <summary>A {WORD} placeholder still literally present in a parameter value: the profile
    /// named no target directory for it (e.g. {FEDERATION_CONFIRMATIONS}), so any node reading
    /// that parameter fails with "file not found" against the placeholder text itself.</summary>
    private static readonly Regex UnresolvedPlaceholder = new("\\{[A-Z_]+\\}", RegexOptions.Compiled);

    /// <summary>The sample files as (id, document with the placeholder rewritten), leaving out
    /// and logging each one that still holds a placeholder or that the registry cannot run.</summary>
    private static IEnumerable<(string Id, GraphDocument Doc)> Samples(IEnumerable<string> files, string placeholder,
        string targetDir, INodeRegistry? registry, TextWriter log)
    {
        foreach (var file in files)
        {
            var id = Path.GetFileNameWithoutExtension(file);
            var doc = RewritePaths(GraphDocumentIO.Load(file), placeholder, targetDir);
            if (FirstUnresolvedPlaceholder(doc) is { } unresolved)
            {
                log.WriteLine($"  skipped sample analysis {id}: parameter still holds unresolved placeholder {unresolved}");
                continue;
            }
            var errors = registry is null ? [] : doc.Validate(registry);
            if (errors.Count > 0)
            {
                log.WriteLine($"  skipped sample analysis {id}: {string.Join("; ", errors.Select(e => e.Message).Distinct())}");
                continue;
            }
            yield return (id, doc);
        }
    }

    /// <summary>The first {WORD} placeholder any parameter value still holds after rewriting,
    /// or null when every placeholder the graph names resolved to a real path.</summary>
    private static string? FirstUnresolvedPlaceholder(GraphDocument doc)
        => doc.Values.Values
            .SelectMany(p => p.Values)
            .Select(v => UnresolvedPlaceholder.Match(v))
            .FirstOrDefault(m => m.Success)
            ?.Value;

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

    /// <summary>The nearest ancestor of startDir containing a solution file, or null.</summary>
    public static string? FindRepoRoot(string startDir)
    {
        for (var dir = new DirectoryInfo(startDir); dir != null; dir = dir.Parent)
            if (dir.EnumerateFiles(SolutionPattern).Any())
                return dir.FullName;
        return null;
    }
}
