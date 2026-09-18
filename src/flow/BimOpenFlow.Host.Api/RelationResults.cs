using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Contracts;

namespace BimOpenFlow.Host.Api;

/// <summary>How the API turns a relation-valued output into rows and column names.
/// Supplied by the host composition, because Host.Api references no node pack;
/// without one, relation outputs report that no executor is attached.</summary>
public interface IRelationResults
{
    /// <summary>One page of the relation's rows plus its unpaged total.</summary>
    TableSlice Slice(RelationValue relation, int skip, int take);

    /// <summary>The relation's columns from its inferred schema, without running it.</summary>
    IReadOnlyList<Suggestion> Columns(RelationValue relation);
}
