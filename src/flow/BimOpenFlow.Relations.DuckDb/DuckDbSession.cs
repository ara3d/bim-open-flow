using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.BimOpenSchema.IO;
using DuckDB.NET.Data;

namespace BimOpenFlow.Relations.DuckDb;

/// <summary>An in-memory DuckDB connection with a query's sources bound under their names:
/// database files attached read-only, CSV folders exposed as a schema of views.</summary>
public sealed class DuckDbSession : IDisposable
{
    public DuckDBConnection Connection { get; } = BosDuckDb.OpenInMemory();
    private readonly HashSet<string> _bound = new(StringComparer.Ordinal);

    /// <summary>A session with the query's sources bound and every inline table written into
    /// schema "_inline".</summary>
    public static DuckDbSession For(IReadOnlyList<SourceUse> sources, IConnectionRegistry registry,
        IReadOnlyList<InlineUse> inlines, IInlineTables tables)
    {
        var session = For(sources, registry);
        try
        {
            session.BindInline(inlines, tables);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public static DuckDbSession For(IReadOnlyList<SourceUse> sources, IConnectionRegistry registry)
    {
        var session = new DuckDbSession();
        try
        {
            foreach (var use in sources) session.Bind(use, registry);
            return session;
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    public void Bind(SourceUse use, IConnectionRegistry registry)
    {
        var location = registry.Require(use.Source);
        switch (location.Type, use.Kind)
        {
            case (SourceType.DuckDbFile, SourceKind.Table):
                if (_bound.Add(use.Source))
                    Connection.Execute($"ATTACH {location.Path.Literal()} AS {use.Source.Ident()} (READ_ONLY)");
                break;
            case (SourceType.FileRoot, SourceKind.Csv):
                if (_bound.Add(use.Source))
                    Connection.Execute($"CREATE SCHEMA {use.Source.Ident()}");
                if (_bound.Add(use.Source + "/" + use.Reference))
                    Connection.Execute($"CREATE VIEW {use.Ident} AS SELECT * FROM read_csv({FileUnder(location.Path, use.Reference).Literal()})");
                break;
            default:
                throw new ArgumentException($"Source '{use.Source}' is a {location.Type} and cannot serve a {use.Kind} reference.");
        }
    }

    /// <summary>Writes the rows behind each inline table into schema "_inline", where the
    /// compiled SQL expects them. <c>DuckDbUtils.WriteTable</c> quotes its table name as a
    /// single identifier and so cannot target a schema; each table is therefore written under
    /// a private name in the session's main schema and exposed under "_inline" as a view.</summary>
    public void BindInline(IReadOnlyList<InlineUse> inlines, IInlineTables tables)
    {
        if (inlines.Count == 0) return;
        Connection.Execute($"CREATE SCHEMA IF NOT EXISTS {CompiledQuery.InlineSchema.Ident()}");
        for (var i = 0; i < inlines.Count; i++)
        {
            var use = inlines[i];
            if (!_bound.Add(CompiledQuery.InlineSchema + "/" + use.Name))
                throw new ArgumentException($"Two different inline tables are named '{use.Name}'.");
            var table = tables.Find(use.Hash)
                ?? throw new ArgumentException($"No rows are registered for inline table '{use.Name}' (hash {use.Hash}).");
            var staged = $"_inline_{i + 1}";
            Connection.WriteTable(table, staged);
            Connection.Execute($"CREATE VIEW {use.Ident} AS SELECT * FROM {staged.Ident()}");
        }
    }

    /// <summary>The file's full path, refusing anything that escapes the root.</summary>
    public static string FileUnder(string root, string relative)
    {
        var full = Path.GetFullPath(Path.Combine(root, relative));
        var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"'{relative}' is outside the source root.");
        if (!File.Exists(full))
            throw new FileNotFoundException($"File not found under source root: {relative}", full);
        return full.Replace('\\', '/');
    }

    public void Dispose()
        => Connection.Dispose();
}
