using System.Globalization;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.Nodes.Tables;

/// <summary>Uniform extraction of node inputs and required parameters,
/// with the node kind in every error message.</summary>
internal static class NodeArgs
{
    public static IDataTable TableInput(this IReadOnlyList<FlowValue> inputs, int index, string kind)
        => index < inputs.Count && inputs[index] is TableValue t
            ? t.Table
            : throw new ArgumentException($"{kind}: input {index} must be a Table.");

    public static string RequiredText(this ParamValues parameters, string name, string kind)
    {
        var text = parameters.GetText(name);
        return !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new ArgumentException($"{kind}: parameter '{name}' is required.");
    }

    public static IReadOnlyList<string> SplitNames(this string text)
        => text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    public static double RequiredNumber(this ParamValues parameters, string name, string kind)
        => double.TryParse(parameters.RequiredText(name, kind).Trim(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"{kind}: parameter '{name}' must be a number.");

    /// <summary>Canonical DateTime form: ISO-8601 date or date-and-time; empty = unset.</summary>
    public static DateTime RequiredDateTime(this ParamValues parameters, string name, string kind)
        => DateTime.TryParseExact(parameters.RequiredText(name, kind).Trim(),
            ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : throw new ArgumentException(
                $"{kind}: parameter '{name}' must be ISO-8601 (yyyy-MM-dd or yyyy-MM-ddTHH:mm:ss).");

    public static string RequiredEnum(this ParamValues parameters, string name, string kind,
        string @default, params string[] allowed)
    {
        var value = parameters.GetText(name, @default);
        return allowed.Contains(value)
            ? value
            : throw new ArgumentException($"{kind}: parameter '{name}' must be one of {string.Join(", ", allowed)}.");
    }
}
