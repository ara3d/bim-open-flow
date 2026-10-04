namespace BimOpenFlow.Host.Catalog;

public readonly record struct ModelInfo(
    int EntityCount,
    int ParameterCount,
    int DocumentCount,
    int RelationCount);
