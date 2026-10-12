using System.Globalization;
using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Relations;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Reads the file formats this pack owns for the schema pass: "xlsx" (options sheet, headerRow,
/// range) and "sqlite" (option table, or sql). Everything else goes to the catalog it wraps, so
/// <c>new TablesCatalog(new DuckDbCatalog(...))</c> answers every reader. Put a caching catalog around it.</summary>
public sealed class TablesCatalog(ISchemaCatalog inner) : ISchemaCatalog
{
    public const string Xlsx = "xlsx";
    public const string Sqlite = "sqlite";

    public SchemaResult CsvSchema(string source, string path) => inner.CsvSchema(source, path);

    public SchemaResult TableSchema(string source, string table) => inner.TableSchema(source, table);

    public SchemaResult QuerySchema(string sql, IReadOnlyList<Schema> inputs) => inner.QuerySchema(sql, inputs);

    public SchemaResult FileQuerySchema(string sql, IReadOnlyList<string> files) => inner.FileQuerySchema(sql, files);

    public SchemaResult FileSchema(string format, string path, IReadOnlyDictionary<string, string> options)
        => format switch
        {
            Xlsx => Attempt(() => SchemaResult.Of(TableSchemas.Of(XlsxReadNode.Read(path,
                Option(options, "sheet"), long.Parse(Option(options, "headerRow", "1"), CultureInfo.InvariantCulture), Option(options, "range"))))),
            Sqlite => Attempt(() => SqliteSchemas.Of(path, options)),
            _ => inner.FileSchema(format, path, options),
        };

    private static string Option(IReadOnlyDictionary<string, string> options, string name, string @default = "")
        => options.GetValueOrDefault(name, @default);

    /// <summary>A file that cannot be opened or a bad option is a failed result, not an exception.</summary>
    private static SchemaResult Attempt(Func<SchemaResult> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return SchemaResult.Fail(e.Message);
        }
    }
}
