using System;
using System.IO;
using System.Linq;
using System.Text;
using Ara3D.DataFlowEngine.Runs;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Store;

namespace BimOpenFlow.Host.Api;

/// <summary>The editor session record: which analysis the editor shows and which nodes are
/// selected. One per store, kept as a JSON file at the store root, so another process on the
/// same store (the stdio MCP server) reads what the host wrote.</summary>
public sealed class EditorSessions(AnalysisStore store)
{
    public const string FileName = ".editor-session.json";
    public static readonly EditorSession None = new(null, [], null);

    public string FilePath => Path.Combine(store.RootDir, FileName);

    /// <summary>The stored record; None when the file is missing or unreadable. Never throws for I/O.</summary>
    public EditorSession Read()
    {
        try
        {
            var text = File.ReadAllText(FilePath, Encoding.UTF8);
            return System.Text.Json.JsonSerializer.Deserialize<EditorSession>(text, ApiJson.Options) ?? None;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            return None;
        }
    }

    /// <summary>Validates the id (AnalysisId.IsValid; empty means none), drops duplicate and empty
    /// selection ids, stamps UpdatedUtc, writes atomically, and returns what was stored.</summary>
    public EditorSession Write(EditorSession session, DateTimeOffset now)
    {
        var analysisId = string.IsNullOrEmpty(session.AnalysisId) ? null : session.AnalysisId;
        if (analysisId != null && !AnalysisId.IsValid(analysisId))
            throw new ArgumentException($"Invalid analysis id '{analysisId}': must be a lowercase slug (a-z, 0-9, interior hyphens)");

        var selection = session.Selection
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var stored = new EditorSession(analysisId, selection, RunTimestamp.Format(now));
        var json = ApiJson.Serialize(stored);
        AtomicFile.WriteAllText(FilePath, json, Encoding.UTF8);
        return stored;
    }

    public void Clear()
        => Write(None, DateTimeOffset.UtcNow);
}
