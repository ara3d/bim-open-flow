using Ara3D.BimOpenSchema.DuckDb;
using DuckDB.NET.Data;

namespace BimOpenFlow.Relations.DuckDb;

/// <summary>An in-memory DuckDB connection with a query's sources bound under their names:
/// database files attached read-only, CSV folders exposed as a schema of views.</summary>
public sealed class DuckDbSession : IDisposable
{
    public DuckDBConnection Connection { get; } = BosDuckDb.OpenInMemory();
    private readonly HashSet<string> _bound = new(StringComparer.Ordinal);

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
