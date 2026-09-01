using Ara3D.BimOpenSchema.DuckDb;
using BimOpenFlow.Contracts;
using BimOpenFlow.Nodes.DuckDb;

namespace BimOpenFlow.Host;

/// <summary>DuckDB-backed FileTableProbe for TablesInFile suggestions: opens the
/// file read-only and lists its table names from information_schema (no scans).</summary>
public static class DuckDbTableProbe
{
    public static IReadOnlyList<Suggestion> Tables(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"File not found: {path}", path);
        using var conn = DuckDbOps.OpenReadOnly(path);
        var names = conn.Query(
            "SELECT table_name FROM information_schema.tables "
            + "WHERE table_schema = 'main' ORDER BY table_name");
        var tables = new List<Suggestion>(names.Rows.Count);
        for (var row = 0; row < names.Rows.Count; row++)
            tables.Add(new((string)names[0, row]!, null));
        return tables;
    }
}
