using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.BimOpenSchema.IO;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>Shared DuckDB plumbing for the file-format sinks: load the flowing
/// table into an in-memory database and COPY it out.</summary>
internal static class DuckWriting
{
    /// <summary>A single-quoted SQL path literal with forward slashes.</summary>
    public static string PathLiteral(string path)
        => "'" + path.Replace('\\', '/').Replace("'", "''") + "'";

    public static void CopyTable(IDataTable table, string outPath, string options)
    {
        using var conn = BosDuckDb.OpenInMemory();
        conn.WriteTable(table, "t");
        conn.Execute($"COPY \"t\" TO {PathLiteral(outPath)} ({options})");
    }
}
