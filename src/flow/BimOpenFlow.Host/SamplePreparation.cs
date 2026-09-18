using Ara3D.Ifc.DuckDb;
using Ara3D.Utils;

namespace BimOpenFlow.Host;

/// <summary>Generated sample data that takes longer than a start-up may: today the NRC
/// DuckDB built from samples/nrc/duplex-enriched.ifc, about 30 s cold. The host opens its
/// port first and runs these jobs afterwards; until a job lands, the relation registry
/// answers "not ready yet" for its source name, and when it lands the caller re-evaluates
/// every open analysis so the waiting nodes recover without a reload.</summary>
public static class SamplePreparation
{
    public const string NrcSourceName = "duplex-enriched";

    /// <summary>Suffix of the file a build writes before it is moved into place, so a
    /// registry scanning for *.duckdb never sees a half-written database.</summary>
    public const string PartSuffix = ".part";

    /// <summary>One generated file: the source name graphs use for it, its input, its
    /// output, and the function that builds the output from the input.</summary>
    public sealed record Job(string Source, string Input, string Output, Action<string, string> Build)
    {
        /// <summary>True when the output exists and is at least as new as the input.</summary>
        public bool IsCurrent
            => File.Exists(Output) && File.GetLastWriteTimeUtc(Output) >= File.GetLastWriteTimeUtc(Input);

        public string Reason
            => $"building {Path.GetFileName(Output)} from {Path.GetFileName(Input)} in the background; it appears when done";

        /// <summary>Builds to the .part sibling, then moves it over the output.</summary>
        public void Run()
        {
            var part = Output + PartSuffix;
            Build(Input, part);
            File.Move(part, Output, overwrite: true);
        }
    }

    /// <summary>The NRC database job for a repo root, or null when the IFC is not there.</summary>
    public static Job? NrcDatabase(string root)
    {
        var dir = SampleSeeding.NrcSamplesDir(root);
        var ifc = Path.Combine(dir, SampleSeeding.NrcIfcFileName);
        return File.Exists(ifc)
            ? new(NrcSourceName, ifc, Path.Combine(dir, SampleSeeding.NrcDatabaseFileName),
                (input, output) => IfcDuckDbBuild.Build(new FilePath(input), new FilePath(output)))
            : null;
    }

    /// <summary>Every job for the checkout that contains startDir; empty outside a checkout.</summary>
    public static IReadOnlyList<Job> Jobs(string startDir)
        => SampleSeeding.FindRepoRoot(startDir) is { } root && NrcDatabase(root) is { } nrc ? [nrc] : [];

    /// <summary>Why a source name cannot be resolved yet, or null when no stale job owns it.</summary>
    public static Func<string, string?> PendingReason(IReadOnlyList<Job> jobs)
        => name => jobs.FirstOrDefault(j => j.Source == name && !j.IsCurrent)?.Reason;

    /// <summary>Runs every stale job in order, calling onReady after each one lands.
    /// A failing job is logged and skipped; the others still run.</summary>
    public static void Run(IReadOnlyList<Job> jobs, Action<Job> onReady, TextWriter log)
    {
        foreach (var job in jobs.Where(j => !j.IsCurrent))
        {
            log.WriteLine($"  preparing {job.Source}: {job.Reason}");
            try
            {
                job.Run();
                log.WriteLine($"  ready: {job.Source} ({Path.GetFileName(job.Output)})");
                onReady(job);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                log.WriteLine($"  failed to prepare {job.Source}: {e.Message}");
            }
        }
    }

    public static Task RunInBackground(IReadOnlyList<Job> jobs, Action<Job> onReady, TextWriter log)
        => jobs.Any(j => !j.IsCurrent) ? Task.Run(() => Run(jobs, onReady, log)) : Task.CompletedTask;
}
