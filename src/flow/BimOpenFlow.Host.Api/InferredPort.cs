using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api;

/// <summary>What the schema pass knows about one Table or Relation output before any run. Schema is set
/// when Status is Ok; Reason says why it is not when Status is Unknown or Dynamic, and names the node
/// that made it so. Errors are the failed inference's messages; Warnings stay on the node that raised them.
/// Kinds carries the column kinds a Static declaration names.</summary>
public sealed record InferredPort(
    string Node,
    string Output,
    SchemaStatus Status,
    Schema? Schema = null,
    string? Reason = null,
    IReadOnlyList<string>? Errors = null,
    IReadOnlyList<string>? Warnings = null,
    IReadOnlyDictionary<string, string>? Kinds = null)
{
    public static InferredPort Of(string node, string output, SchemaResult result, IReadOnlyDictionary<string, string>? kinds = null)
        => result.Ok
            ? new(node, output, SchemaStatus.Ok, result.Schema, null, null, result.Warnings.Select(w => w.Message).ToList(), kinds)
            : new(node, output, SchemaStatus.Error, null, null, result.Errors.Select(e => e.Message).ToList());

    public static InferredPort Unknown(string node, string output, string reason)
        => new(node, output, SchemaStatus.Unknown, Reason: reason);

    public static InferredPort Dynamic(string node, string output, string reason)
        => new(node, output, SchemaStatus.Dynamic, Reason: reason);
}
