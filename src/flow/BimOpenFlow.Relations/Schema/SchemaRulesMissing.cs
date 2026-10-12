namespace BimOpenFlow.Relations;

/// <summary>How the rules report a column that a parameter names and the input lacks: one message that
/// offers the nearest existing column, and one way to split a list of names into present and absent.
/// A rule whose run fails on an absent column fails with the message; a rule whose run warns and skips
/// it (table.project, table.drop, table.rename) keeps the schema the run produces and carries the
/// message as a warning.</summary>
public static partial class SchemaRules
{
    /// <summary>The existing name closest to the missing one, or null when none is close. Compared
    /// ignoring case; a name is close when its edit distance is at most a third of the missing name's
    /// length, and at least 2. Ties go to the earlier column.</summary>
    public static string? NearestName(IEnumerable<string> existing, string missing)
    {
        var limit = Math.Max(2, missing.Length / 3);
        string? best = null;
        var bestDistance = int.MaxValue;
        foreach (var name in existing)
        {
            var distance = EditDistance(name.ToLowerInvariant(), missing.ToLowerInvariant());
            if (distance <= limit && distance < bestDistance)
                (best, bestDistance) = (name, distance);
        }
        return best;
    }

    /// <summary>"No column named 'X'." plus the nearest existing column when there is one.</summary>
    public static string NoColumnMessage(Schema schema, string column)
        => NearestName(schema.Columns.Select(c => c.Name), column) is { } near
            ? $"No column named '{column}'; did you mean '{near}'?"
            : $"No column named '{column}'.";

    /// <summary>The items whose column exists, and the warnings for those whose column does not.</summary>
    private static (List<T> Present, List<SchemaError> Warnings) SplitMissing<T>(Schema schema, IEnumerable<T> items, Func<T, string> column)
    {
        var (present, warnings) = (new List<T>(), new List<SchemaError>());
        foreach (var item in items)
            if (schema.Has(column(item))) present.Add(item);
            else warnings.Add(new SchemaError(NoColumnMessage(schema, column(item))));
        return (present, warnings);
    }

    private static int EditDistance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1], previous[j]) + 1, previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            previous = current;
        }
        return previous[b.Length];
    }
}
