using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace BimOpenFlow.Host;

/// <summary>Generated sample data that takes longer than a start-up may (the studio's NRC
/// DuckDBs, built from IFC files, take about 30 s each cold). A profile names its jobs; the
/// host opens its port first and runs them afterwards; until a job lands, the relation registry
/// answers "not ready yet" for its source name, and when it lands the caller re-evaluates
/// every open analysis so the waiting nodes recover without a reload.
///
/// An output is current only when it was produced from the same input by the same producer.
/// Each build writes a stamp file beside its output naming the producer version and the
/// input's SHA-256; a missing or different stamp means the output is rebuilt. Timestamps are
/// not consulted: a converter change leaves the input untouched, and a fresh checkout gives
/// every file a new time.</summary>
public static class SamplePreparation
{
    /// <summary>Suffix of the file a build writes before it is moved into place, so a
    /// registry scanning for *.duckdb never sees a half-written database.</summary>
    public const string PartSuffix = ".part";

    /// <summary>Suffix of the stamp file written beside each output.</summary>
    public const string StampSuffix = ".stamp";

    /// <summary>Version of the stamp's own layout; changing it rebuilds every output once.</summary>
    public const string StampFormat = "sample-preparation-stamp/1";

    /// <summary>The producer version of a job that names none. Its outputs follow input
    /// changes only; a job whose builder can change its output should pass its own version.</summary>
    public const string UnversionedProducer = "unversioned";

    /// <summary>One generated file: the source name graphs use for it, its input, its output,
    /// the function that builds the output from the input, and the version of that function.
    /// Bump <paramref name="Producer"/> whenever the builder's output would change for the
    /// same input; an explicit constant, because assembly identities change on every build.</summary>
    public sealed record Job(
        string Source, string Input, string Output, Action<string, string> Build,
        string Producer = UnversionedProducer)
    {
        public string StampPath => Output + StampSuffix;

        /// <summary>True when the output exists and its stamp matches the current input and
        /// producer.</summary>
        public bool IsCurrent
            => File.Exists(Output) && File.Exists(Input) && File.Exists(StampPath)
               && File.ReadAllText(StampPath) == Stamp();

        public string Reason
            => $"building {Path.GetFileName(Output)} from {Path.GetFileName(Input)} in the background; it appears when done";

        /// <summary>Builds to the .part sibling, moves it over the output, then writes the
        /// stamp. The old stamp is removed first, so a build interrupted after the move leaves
        /// no stamp and is rebuilt rather than trusted.</summary>
        public void Run()
        {
            var stamp = Stamp();
            var part = Output + PartSuffix;
            Build(Input, part);
            File.Delete(StampPath);
            File.Move(part, Output, overwrite: true);
            File.WriteAllText(StampPath + PartSuffix, stamp);
            File.Move(StampPath + PartSuffix, StampPath, overwrite: true);
        }

        private string Stamp()
            => $"{StampFormat}\nproducer: {Producer}\ninput: {Path.GetFileName(Input)} sha256:{InputHashes.Of(Input)}\n";
    }

    /// <summary>SHA-256 of a file, remembered while its length and write time stay the same, so
    /// the registry's repeated "is it ready" checks do not reread the input each time.</summary>
    private static class InputHashes
    {
        private static readonly ConcurrentDictionary<string, (long Length, DateTime Written, string Hash)> Known = new();

        public static string Of(string path)
        {
            var info = new FileInfo(path);
            var key = info.FullName;
            if (Known.TryGetValue(key, out var k) && k.Length == info.Length && k.Written == info.LastWriteTimeUtc)
                return k.Hash;
            using var stream = info.OpenRead();
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            Known[key] = (info.Length, info.LastWriteTimeUtc, hash);
            return hash;
        }
    }

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
