namespace BimOpenFlow.Relations;

/// <summary>The only design-time I/O: what columns a source has. Implementations may read a
/// CSV header, query information_schema, or answer from a dictionary in tests.</summary>
public interface ICatalog
{
    SchemaResult CsvSchema(string source, string path);
    SchemaResult TableSchema(string source, string table);

    /// <summary>The result schema of user SQL whose inputs t1..tN have the given schemas.</summary>
    SchemaResult QuerySchema(string sql, IReadOnlyList<Schema> inputs);
}

/// <summary>A catalog answered from fixed tables, for tests and for graphs whose
/// sources are declared rather than probed. Source keys are "source/reference";
/// raw SQL is keyed by its exact text.</summary>
public sealed class StaticCatalog(IReadOnlyDictionary<string, Schema> entries, IReadOnlyDictionary<string, Schema>? queries = null) : ICatalog
{
    public static string Key(string source, string reference) => $"{source}/{reference}";

    public SchemaResult CsvSchema(string source, string path) => Lookup(source, path);
    public SchemaResult TableSchema(string source, string table) => Lookup(source, table);

    public SchemaResult QuerySchema(string sql, IReadOnlyList<Schema> inputs)
        => queries is not null && queries.TryGetValue(sql, out var schema)
            ? SchemaResult.Of(schema)
            : SchemaResult.Fail("Raw SQL cannot be typed without a database catalog.");

    private SchemaResult Lookup(string source, string reference)
        => entries.TryGetValue(Key(source, reference), out var schema)
            ? SchemaResult.Of(schema)
            : SchemaResult.Fail($"Unknown source '{source}/{reference}'.");
}
