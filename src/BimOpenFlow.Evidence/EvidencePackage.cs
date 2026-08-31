using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Ara3D.DataFlowEngine.Runs;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Evidence;

/// <summary>One pinned input snapshot to include under inputs/ in the package.</summary>
public sealed record EvidenceInput(string Name, byte[] Content)
{
    public string Name { get; } = IsSafeName(Name)
        ? Name
        : throw new ArgumentException($"Invalid input name '{Name}': must be a plain file name", nameof(Name));

    public static EvidenceInput FromFile(string path)
        => new(Path.GetFileName(path), File.ReadAllBytes(path));

    private static bool IsSafeName(string name)
        => name.Length > 0 && name != "." && name != ".."
           && !name.Contains('/') && !name.Contains('\\') && !name.Contains(':');
}

public sealed record VerifyResult(bool Ok, IReadOnlyList<string> Mismatches);

/// <summary>
/// The compliance hand-off archive: one .zip holding the canonical graph, the
/// run record, the rendered report, pinned input snapshots, and a canonical
/// manifest.json hashing every member. Build writes it; Verify re-hashes the
/// members against the manifest.
/// </summary>
public static class EvidencePackage
{
    public const string PackageVersion = "0.1.0";
    public const string ManifestName = "manifest.json";
    public const string GraphName = "graph.dfg.json";
    public const string RunName = "run.run.json";
    public const string ReportName = "report.html";
    public const string InputsFolder = "inputs/";

    private static readonly Encoding Utf8 = new UTF8Encoding(false);

    /// <summary>
    /// Builds the package at outPath. createdUtc is passed in by the caller
    /// (RFC 3339 UTC, e.g. RunTimestamp.Format) so building is deterministic.
    /// The graph must be the one the run was frozen from.
    /// </summary>
    public static EvidenceManifest Build(
        GraphDocument graph,
        RunRecord run,
        string reportHtml,
        IReadOnlyList<EvidenceInput> inputs,
        string createdUtc,
        string outPath)
    {
        if (!RunTimestamp.IsValid(createdUtc))
            throw new ArgumentException($"'{createdUtc}' is not RFC 3339 UTC with millisecond precision", nameof(createdUtc));
        var graphHash = graph.ComputeGraphHash();
        if (graphHash != run.GraphHash)
            throw new ArgumentException(
                $"Graph hash {graphHash} does not match the run's graph hash {run.GraphHash}", nameof(graph));

        var members = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [GraphName] = Utf8.GetBytes(graph.ToCanonicalJson()),
            [RunName] = Utf8.GetBytes(run.ToCanonicalJson()),
            [ReportName] = Utf8.GetBytes(reportHtml),
        };
        foreach (var input in inputs)
        {
            var name = InputsFolder + input.Name;
            if (members.ContainsKey(name))
                throw new ArgumentException($"Duplicate input name '{input.Name}'", nameof(inputs));
            members[name] = input.Content;
        }

        var manifest = new EvidenceManifest(
            PackageVersion,
            createdUtc,
            run.GraphHash,
            RunName,
            members.ToDictionary(m => m.Key, m => Hashes.HashBytes(m.Value)));

        WriteZip(outPath, manifest, members);
        return manifest;
    }

    /// <summary>Re-hashes every member against the manifest; reports hash
    /// mismatches, members missing from the archive, and unlisted extras.</summary>
    public static VerifyResult Verify(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var manifestEntry = zip.GetEntry(ManifestName)
            ?? throw new FormatException($"Package has no {ManifestName}");
        var manifest = EvidenceManifest.Parse(ReadText(manifestEntry));

        var mismatches = new List<string>();
        if (manifest.PackageVersion != PackageVersion)
            mismatches.Add($"unsupported packageVersion '{manifest.PackageVersion}'");

        foreach (var (name, expected) in manifest.Files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            var entry = zip.GetEntry(name);
            if (entry is null)
                mismatches.Add($"missing member '{name}'");
            else if (Hashes.HashBytes(ReadBytes(entry)) is var actual && actual != expected)
                mismatches.Add($"hash mismatch for '{name}': manifest {expected}, actual {actual}");
        }
        foreach (var entry in zip.Entries)
            if (entry.Name.Length > 0 && entry.FullName != ManifestName && !manifest.Files.ContainsKey(entry.FullName))
                mismatches.Add($"unlisted member '{entry.FullName}'");

        return new(mismatches.Count == 0, mismatches);
    }

    private static void WriteZip(string outPath, EvidenceManifest manifest, IReadOnlyDictionary<string, byte[]> members)
    {
        var stamp = DateTimeOffset.Parse(manifest.Created, System.Globalization.CultureInfo.InvariantCulture);
        using var stream = File.Create(outPath);
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create);
        AddEntry(zip, ManifestName, Utf8.GetBytes(manifest.ToCanonicalJson()), stamp);
        foreach (var (name, content) in members)
            AddEntry(zip, name, content, stamp);
    }

    private static void AddEntry(ZipArchive zip, string name, byte[] content, DateTimeOffset stamp)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = stamp;
        using var target = entry.Open();
        target.Write(content);
    }

    private static string ReadText(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Utf8);
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(ZipArchiveEntry entry)
    {
        using var source = entry.Open();
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray();
    }
}
