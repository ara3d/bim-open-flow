using System.Text.RegularExpressions;

namespace BimOpenFlow.Nodes.Support;

/// <summary>Lets a schema pass type a query written against the table <c>t</c> when the catalog's
/// first input is named <c>t1</c>.</summary>
public static partial class SqlAlias
{
    /// <summary>The query with the CTE <c>t AS (SELECT * FROM t1)</c> in front, joined to the query's own
    /// WITH clause when it has one.</summary>
    public static string WithT(string sql)
    {
        const string alias = "t AS (SELECT * FROM t1)";
        var with = WithKeyword().Match(sql);
        return with.Success
            ? $"{with.Value}{alias}, {sql[with.Length..]}"
            : $"WITH {alias} {sql}";
    }

    [GeneratedRegex(@"^\s*WITH\s+(RECURSIVE\s+)?", RegexOptions.IgnoreCase)]
    private static partial Regex WithKeyword();
}
