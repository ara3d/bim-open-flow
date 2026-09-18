namespace BimOpenFlow.Relations;

public readonly record struct SchemaError(string Message)
{
    public override string ToString() => Message;
}

/// <summary>Either a schema or the errors that prevented one. Errors are values, never
/// exceptions, so the editor can show them on a node before anything runs.</summary>
public sealed record SchemaResult(Schema? Schema, IReadOnlyList<SchemaError> Errors)
{
    public bool Ok => Schema is not null && Errors.Count == 0;

    public static SchemaResult Of(Schema schema) => new(schema, []);
    public static SchemaResult Of(params Column[] columns) => Of(new Schema(columns));
    public static SchemaResult Fail(string message) => new(null, [new SchemaError(message)]);
    public static SchemaResult Fail(IReadOnlyList<SchemaError> errors) => new(null, errors);

    public Schema Require()
        => Ok ? Schema! : throw new InvalidOperationException(string.Join("; ", Errors));

    public override string ToString()
        => Ok ? Schema!.ToString() : string.Join("; ", Errors);
}
