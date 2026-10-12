namespace BimOpenFlow.Relations;

/// <summary>Plumbing every <see cref="ISchemaNode"/> over tables shares: it turns the parallel
/// <see cref="SchemaInput"/> list into plain schemas, passes an upstream failure along unchanged,
/// and turns a parameter that does not parse into a failed result, so Infer never throws.</summary>
public static class SchemaInputs
{
    /// <summary>Runs the rule over the schemas of every input port. An input that is not connected fails
    /// naming the port; an input whose own inference failed hands its failure on.</summary>
    /// <param name="throughDuckDb">True for a node that loads its inputs into DuckDB and reads the result back
    /// (DuckTableSql.Run). The read-back turns Date and Timestamp columns into ISO text, so the rule sees them as Text.</param>
    public static SchemaResult Run(NodeSpec spec, IReadOnlyList<SchemaInput> inputs, Func<IReadOnlyList<Schema>, SchemaResult> rule,
        bool throughDuckDb = false)
    {
        var schemas = new List<Schema>();
        for (var i = 0; i < inputs.Count; i++)
        {
            if (!inputs[i].Connected || inputs[i].Schema is null)
                return SchemaResult.Fail($"Input '{spec.Inputs[i].Name}' is not connected.");
            if (!inputs[i].Schema!.Ok)
                return inputs[i].Schema!;
            schemas.Add(throughDuckDb ? DatesAsText(inputs[i].Schema!.Schema!) : inputs[i].Schema!.Schema!);
        }
        try
        {
            return rule(schemas);
        }
        catch (ArgumentException e)
        {
            return SchemaResult.Fail(e.Message);
        }
    }

    private static Schema DatesAsText(Schema schema)
        => schema.Map(c => c.Type is ColumnType.Date or ColumnType.Timestamp ? c with { Type = ColumnType.Text } : c);
}
