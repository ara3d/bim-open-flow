using System;
using System.Text.RegularExpressions;

namespace BimOpenFlow.Host.Store;

/// <summary>
/// Analysis ids are lowercase slugs: letters, digits, and interior hyphens.
/// They double as directory names, so dots, slashes, and uppercase are refused.
/// </summary>
public static class AnalysisId
{
    private static readonly Regex Pattern = new(
        "^[a-z0-9]([a-z0-9-]*[a-z0-9])?$", RegexOptions.Compiled);

    public static bool IsValid(string id)
        => Pattern.IsMatch(id);

    public static string Validate(string id)
        => IsValid(id)
            ? id
            : throw new ArgumentException(
                $"Invalid analysis id '{id}': must be a lowercase slug (a-z, 0-9, interior hyphens)", nameof(id));
}
