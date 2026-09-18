namespace BimOpenFlow.Host.Catalog;

/// <summary>One resolved BOS parameter: every index already dereferenced to text,
/// so the value is displayable without touching the model again.</summary>
public readonly record struct ModelEntityParameter(
    string Group,
    string Name,
    string Value,
    string? Units);

/// <summary>An entity and its parameters, sorted by group then name.
/// LocalId is the STEP express id for a BOS converted from IFC.</summary>
public sealed record ModelEntity(
    long LocalId,
    string? GlobalId,
    string? Name,
    string? Category,
    IReadOnlyList<ModelEntityParameter> Parameters);
