using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Host.Store;

/// <summary>
/// The caller's current values for an analysis's graph inputs (format part 3a), kept beside the
/// document in <c>graph-inputs.json</c>: a JSON object of input name to canonical string. The
/// document holds the declarations and defaults; changing a value here never changes the graph
/// hash, so one graph run on two buildings stays one graph. A missing or unreadable file means
/// no values, which is every input at its default.
/// </summary>
public static class AnalysisStoreInputs
{
    public static IReadOnlyDictionary<string, string> LoadGraphInputs(this AnalysisStore store, string id)
    {
        var path = store.GraphInputsPath(id);
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(
                File.ReadAllText(path, GraphDocumentIO.Utf8NoBom));
            return parsed ?? new Dictionary<string, string>();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>Writes the values atomically, sorted by name; an empty map removes the file.</summary>
    public static void SaveGraphInputs(this AnalysisStore store, string id, IReadOnlyDictionary<string, string> values)
    {
        var path = store.GraphInputsPath(id);
        if (values.Count == 0)
        {
            File.Delete(path);
            return;
        }
        var sorted = new SortedDictionary<string, string>(values.ToDictionary(kv => kv.Key, kv => kv.Value),
            StringComparer.Ordinal);
        AtomicFile.WriteAllText(path,
            JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true }) + "\n",
            GraphDocumentIO.Utf8NoBom);
    }
}
