namespace BimOpenFlow.Ask;

/// <summary>Reads an API key from an environment variable, or from the first non-blank line
/// of the file another variable names. The file form keeps the key out of shell histories
/// and process listings. Shared by every provider client.</summary>
public static class ApiKeys
{
    /// <summary>Null when neither variable is set; throws when the file variable names a
    /// missing file, because a typo there should not silently look like 'no key'.</summary>
    public static string? Resolve(string keyVariable, string fileVariable, Func<string, string?>? environment = null)
    {
        var env = environment ?? Environment.GetEnvironmentVariable;
        var key = env(keyVariable);
        if (!string.IsNullOrWhiteSpace(key))
            return key.Trim();
        var file = env(fileVariable);
        if (string.IsNullOrWhiteSpace(file))
            return null;
        if (!File.Exists(file))
            throw new FileNotFoundException($"{fileVariable} points to a missing file: {file}", file);
        var line = File.ReadLines(file).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
        return string.IsNullOrEmpty(line) ? null : line;
    }

    /// <summary>A trimmed environment value, or the fallback when unset or blank.</summary>
    public static string ValueOr(string variable, string fallback, Func<string, string?>? environment = null)
    {
        var value = (environment ?? Environment.GetEnvironmentVariable)(variable);
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
