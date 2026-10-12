using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ara3D.NodeGraph;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host;

/// <summary>What one seeded analysis was seeded from: the sample folder's name (e.g.
/// "bim-analyses") and GraphSha256, the graph hash (structure and values, not layout) of the
/// sample as seeded, after placeholder substitution. A record written before 2026-10-11 holds
/// Sha256 instead, the hash of the whole document with its layout; it is read, never written.
/// An empty hash stands for "seeded from a version nobody recorded".</summary>
public sealed record SampleSeed(string Source, string? GraphSha256, string? Sha256 = null)
{
    /// <summary>True when this record describes exactly what doc holds, apart from layout for
    /// a current record; a legacy record compares the whole document.</summary>
    public bool IsSeedOf(GraphDocument doc)
        => GraphSha256 is { } graph
            ? graph == SampleSeedRecord.Hash(doc)
            : Sha256 == SampleSeedRecord.WholeDocumentHash(doc);

    /// <summary>True for a record written before the hash left layout out.</summary>
    public bool IsLegacy => GraphSha256 is null;

    /// <summary>True for a record that names no seeded hash: the store's copy predates the
    /// record and had already been saved over when the record was first written.</summary>
    public bool IsUnrecorded => string.IsNullOrEmpty(GraphSha256 ?? Sha256);
}

/// <summary>
/// The store's record of which analyses came from a sample and what each held when
/// seeded, kept in &lt;store&gt;/.samples.json as { id: { source, graphSha256 } }. A copy whose
/// graph hash still matches its record is the untouched sample, however the user moved its
/// nodes, and may be refreshed; one whose structure or values differ was edited and is kept.
/// scripts/seed-store.mjs in the toolkit reads and writes the same file for the DuckDB
/// studio's workflows, so each writer keeps the other's entries.
/// </summary>
public static class SampleSeedRecord
{
    public const string FileName = ".samples.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
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

    /// <summary>The graph hash the record keeps: SHA-256 of the structure and values only, so
    /// dragging or resizing a node leaves a copy untouched.</summary>
    public static string Hash(GraphDocument doc)
        => doc.ComputeGraphHash();

    /// <summary>SHA-256 (lower-case hex) of the whole document's canonical JSON, layout
    /// included: what a legacy record's Sha256 holds.</summary>
    public static string WholeDocumentHash(GraphDocument doc)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(doc.ToCanonicalJson()))).ToLowerInvariant();
}
