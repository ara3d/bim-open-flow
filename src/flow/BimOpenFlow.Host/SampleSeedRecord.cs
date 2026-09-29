using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host;

/// <summary>What one seeded analysis was seeded from: the sample folder's name (e.g.
/// "bim-analyses") and the SHA-256 of the graph as seeded, after placeholder substitution.</summary>
public sealed record SampleSeed(string Source, string Sha256);

/// <summary>
/// The store's record of which analyses came from a sample and what each held when
/// seeded, kept in &lt;store&gt;/.samples.json as { id: { source, sha256 } }. A copy whose
/// hash still matches its record is the untouched sample and may be refreshed; one that
/// differs was edited and is kept. scripts/seed-store.mjs reads and writes the same file
/// for the DuckDB studio's workflows, so each writer keeps the other's entries.
/// </summary>
public static class SampleSeedRecord
{
    public const string FileName = ".samples.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static string PathOf(AnalysisStore store)
        => Path.Combine(store.RootDir, FileName);

    public static Dictionary<string, SampleSeed> Read(AnalysisStore store)
        => File.Exists(PathOf(store))
            ? JsonSerializer.Deserialize<Dictionary<string, SampleSeed>>(File.ReadAllText(PathOf(store)), Options)
              ?? new()
            : new();

    public static void Write(AnalysisStore store, IReadOnlyDictionary<string, SampleSeed> seeds)
        => AtomicFile.WriteAllText(PathOf(store),
            JsonSerializer.Serialize(new SortedDictionary<string, SampleSeed>(seeds.ToDictionary(), StringComparer.Ordinal), Options) + "\n",
            GraphDocumentIO.Utf8NoBom);

    /// <summary>SHA-256 (lower-case hex) of the document's canonical JSON, the form the store
    /// writes, so a copy the store re-saved without change hashes the same.</summary>
    public static string Hash(GraphDocument doc)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(doc.ToCanonicalJson()))).ToLowerInvariant();
}
