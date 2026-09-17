using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Evidence;

/// <summary>
/// The manifest.json of an evidence package: format version, caller-supplied
/// creation timestamp, graph hash, the run member's file name, and every other
/// member file with its SHA-256. Serialized as canonical JSON so the manifest
/// bytes are deterministic and signable.
/// TODO: signing/attestation over the canonical manifest bytes.
/// </summary>
public sealed record EvidenceManifest(
    string PackageVersion,
    string Created,
    string GraphHash,
    string RunFile,
    IReadOnlyDictionary<string, string> Files)
{
    public string ToCanonicalJson()
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WriteString("packageVersion", PackageVersion);
            w.WriteString("created", Created);
            w.WriteString("graphHash", GraphHash);
            w.WriteString("runFile", RunFile);
            w.WritePropertyName("files");
            w.WriteStartObject();
            foreach (var (name, hash) in Files.OrderBy(f => f.Key, StringComparer.Ordinal))
                w.WriteString(name, hash);
            w.WriteEndObject();
            w.WriteEndObject();
        }
        using var doc = JsonDocument.Parse(stream.ToArray());
        return CanonicalJson.ToCanonicalString(doc.RootElement) + "\n";
    }

    public static EvidenceManifest Parse(string text)
    {
        using var parsed = JsonDocument.Parse(text);
        var root = parsed.RootElement;
        string? version = null, created = null, graphHash = null, runFile = null;
        var files = new Dictionary<string, string>();
        foreach (var p in root.EnumerateObject())
            switch (p.Name)
            {
                case "packageVersion": version = p.Value.GetString(); break;
                case "created": created = p.Value.GetString(); break;
                case "graphHash": graphHash = p.Value.GetString(); break;
                case "runFile": runFile = p.Value.GetString(); break;
                case "files":
                    foreach (var f in p.Value.EnumerateObject())
                        files[f.Name] = f.Value.GetString()
                            ?? throw new FormatException($"File hash for '{f.Name}' must be a string");
                    break;
                default: throw new FormatException($"Unknown manifest member '{p.Name}'");
            }
        return new(
            Required(version, "packageVersion"),
            Required(created, "created"),
            Required(graphHash, "graphHash"),
            Required(runFile, "runFile"),
            files);
    }

    private static string Required(string? value, string name)
        => value ?? throw new FormatException($"Missing required manifest member '{name}'");
}
