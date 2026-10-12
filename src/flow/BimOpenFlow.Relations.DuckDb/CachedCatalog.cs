using System.Collections.Concurrent;

namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Remembers a catalog's answers: SQL by its text and input schemas, files by path, options,
/// length and write time, so an edited file is read again and an untouched one is not. A file that
/// is missing or a glob has no identity and always goes through. Source-registry lookups are not
/// cached; the relation layer's SchemaCache holds those.</summary>
public sealed class CachedCatalog(ISchemaCatalog inner, int capacity = 512) : ISchemaCatalog
{
    private readonly ConcurrentDictionary<string, SchemaResult> _answers = new();

    public int Count => _answers.Count;

    public SchemaResult CsvSchema(string source, string path) => inner.CsvSchema(source, path);

    public SchemaResult TableSchema(string source, string table) => inner.TableSchema(source, table);

    public SchemaResult QuerySchema(string sql, IReadOnlyList<Schema> inputs)
        => Remember($"q|{sql}|{string.Join(";", inputs.Select(Describe))}", () => inner.QuerySchema(sql, inputs));

    public SchemaResult FileQuerySchema(string sql, IReadOnlyList<string> files)
        => Identity(files) is { } identity
            ? Remember($"fq|{sql}|{identity}", () => inner.FileQuerySchema(sql, files))
            : inner.FileQuerySchema(sql, files);

    public SchemaResult FileSchema(string format, string path, IReadOnlyDictionary<string, string> options)
        => Identity([path]) is { } identity
            ? Remember($"f|{format}|{StaticCatalog.FileKey(format, path, options)}|{identity}", () => inner.FileSchema(format, path, options))
            : inner.FileSchema(format, path, options);

    private SchemaResult Remember(string key, Func<SchemaResult> compute)
    {
        if (_answers.TryGetValue(key, out var known)) return known;
        var answer = compute();
        if (_answers.Count >= capacity) _answers.Clear();
        _answers[key] = answer;
        return answer;
    }

    private static string Describe(Schema schema)
        => string.Join(",", schema.Columns.Select(c => $"{c.Name}:{c.Type}"));

    /// <summary>Length and write time of every file, or null when there is none or one has no identity.</summary>
    private static string? Identity(IReadOnlyList<string> files)
    {
        if (files.Count == 0) return null;
        var parts = new List<string>(files.Count);
        foreach (var file in files)
        {
            if (file.IndexOfAny(['*', '?', '[']) >= 0 || new FileInfo(file) is not { Exists: true } info) return null;
            parts.Add($"{info.FullName}={info.Length}/{info.LastWriteTimeUtc.Ticks}");
        }
        return string.Join("|", parts);
    }
}
