using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;

namespace BimOpenFlow.GraphText;

/// <summary>The digest of one output value, by value kind. A table gets its shape, columns,
/// and content hash, then every specialised digest that recognises it; sample rows only when none does.</summary>
public static class ValueText
{
    public static Digest Of(FlowValue value, DigestContext context)
        => value switch
        {
            BooleanValue b => new($"Boolean {(b.Value ? "true" : "false")}", []),
            IntegerValue i => new($"Integer {i.Value}", []),
            NumberValue n => new($"Number {context.Number(n.Value)}", []),
            TextValue t => new($"Text {Literals.QuoteShort(t.Value, Literals.MaxScalarText)}", []),
            TableValue t => Table(t.Table, context),
            RelationValue r => Relation(r, context),
            _ => new(value.Kind.ToString(), []),
        };

    public static Digest Table(IDataTable table, DigestContext context)
        => Tabular("table", table, "", context);

    private static Digest Tabular(string noun, IDataTable table, string suffix, DigestContext context)
        => new($"{noun} {TableText.Shape(table)}  sha {TableText.ContentHash(table, context.Options)}{suffix}",
            [TableText.Columns(table, context.Options), .. Rows(table, context)]);

    /// <summary>Specialised digests in a fixed order; a table no digest recognises shows its first rows.</summary>
    public static IReadOnlyList<string> Special(IDataTable table, DigestContext context)
        => [];

    private static IReadOnlyList<string> Rows(IDataTable table, DigestContext context)
        => Special(table, context) is { Count: > 0 } special ? special : TableText.SampleRows(table, context.Options);

    /// <summary>With a reader: the row count, then every row up to RelationRowCap (hashed and
    /// digested like a table) or a sample beyond it. Without one: the plan only.</summary>
    public static Digest Relation(RelationValue relation, DigestContext context)
    {
        var plan = $"plan {relation.Hash[..Math.Min(TableText.ShortHashLength, relation.Hash.Length)]}";
        if (context.Options.Relations is not { } reader)
            return new($"relation {plan}", [Literals.QuoteShort(relation.Text, Literals.MaxScalarText)]);
        try
        {
            var count = reader.Count(relation);
            if (count > context.Options.RelationRowCap)
            {
                var sample = reader.Rows(relation, context.Options.SampleRows);
                return new($"relation {count} rows x {sample.Columns.Count} cols  {plan}  (over {context.Options.RelationRowCap}, not hashed)",
                    [TableText.Columns(sample, context.Options), .. TableText.SampleRows(sample, context.Options)]);
            }
            return Tabular("relation", reader.Rows(relation, count), $"  {plan}", context);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return new($"relation {plan}  rows unavailable: {Literals.OneLine(e.Message)}", []);
        }
    }
}
