using System.Globalization;
using Ara3D.Ifc.Tests;

namespace BimOpenFlow.Nodes.Effects;

/// <summary>
/// Maps one <c>valueType</c> cell of a sink.writePsets row to the matching IFC value.
/// Names are case-insensitive, an empty name means Text, and Real is a synonym for Number
/// because the source CSVs spell it that way. Literals are parsed with the invariant culture.
/// </summary>
internal static class PsetValueTypes
{
    private static readonly IReadOnlyDictionary<string, Func<string, string, int, IfcPropertyValue>> Cases =
        new Dictionary<string, Func<string, string, int, IfcPropertyValue>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Text"] = (name, text, _) => IfcPropertyValue.Text(name, text),
            ["Label"] = (name, text, _) => IfcPropertyValue.Label(name, text),
            ["Identifier"] = (name, text, _) => IfcPropertyValue.Identifier(name, text),
            ["Integer"] = (name, text, row) => IfcPropertyValue.Integer(name, Integer(text, row)),
            ["Number"] = (name, text, row) => IfcPropertyValue.Real(name, Real(text, row)),
            ["Real"] = (name, text, row) => IfcPropertyValue.Real(name, Real(text, row)),
            ["Boolean"] = (name, text, row) => IfcPropertyValue.Boolean(name, Boolean(text, row)),
        };

    /// <summary>The accepted valueType names, for error messages and documentation.</summary>
    public static IReadOnlyList<string> Names
        => Cases.Keys.ToList();

    public static IfcPropertyValue Create(string? valueType, string name, string text, int row)
        => string.IsNullOrWhiteSpace(valueType)
            ? IfcPropertyValue.Text(name, text)
            : Cases.TryGetValue(valueType.Trim(), out var create)
                ? create(name, text, row)
                : throw new ArgumentException(
                    $"Row {row}: unknown valueType '{valueType}'; expected one of [{string.Join(", ", Names)}]");

    private static long Integer(string text, int row)
        => long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"Row {row}: '{text}' is not an Integer");

    private static double Real(string text, int row)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"Row {row}: '{text}' is not a Number");

    private static bool Boolean(string text, int row)
        => bool.TryParse(text.Trim(), out var value)
            ? value
            : throw new ArgumentException($"Row {row}: '{text}' is not a Boolean (expected true or false)");
}
