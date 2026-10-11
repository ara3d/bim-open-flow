using Ara3D.BimOpenSchema.IO;

namespace BimOpenFlow.Host.Catalog;

/// <summary>Row counts of the Entities, Parameters, Documents, and Relations tables of a BOS model.</summary>
public readonly record struct ModelInfo(
    int EntityCount,
    int ParameterCount,
    int DocumentCount,
    int RelationCount)
{
    /// <summary>The tables a read must include to count these four; geometry is never read.</summary>
    public const BosTables TablesRead = BosTables.Entities | BosTables.Parameters | BosTables.Documents | BosTables.Relations;
}
