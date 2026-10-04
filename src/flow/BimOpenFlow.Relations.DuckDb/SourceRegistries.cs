namespace BimOpenFlow.Relations.DuckDb;

/// <summary>Registries derived from folders: each root is a CSV source named after the
/// folder, and every .duckdb file directly inside a root is a database source named
/// after the file. A later root wins on a name clash.</summary>
public static class SourceRegistries
{
    public const string DuckDbExtension = ".duckdb";

    public static ConnectionRegistry FromRoots(IReadOnlyList<string> roots)
    {
        var entries = new Dictionary<string, SourceLocation>();
        foreach (var root in roots.Where(Directory.Exists).Select(Path.GetFullPath))
        {
            entries[Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar))] = new(SourceType.FileRoot, root);
            foreach (var file in Directory.EnumerateFiles(root, "*" + DuckDbExtension))
                entries[Path.GetFileNameWithoutExtension(file)] = new(SourceType.DuckDbFile, file);
        }
        return new(entries);
    }
}
