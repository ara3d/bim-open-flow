using Ara3D.DataFlowEngine.Abstractions;
using BimOpenFlow.Host.Store;
using BimOpenFlow.Nodes.Relations;

namespace BimOpenFlow.Host;

/// <summary>
/// One named set of node packs and the sample data that goes with it. The host knows none
/// by name except its own generic "tables" profile; whoever composes the host (the
/// bimopenflow-host program, the studio) hands it the profiles it offers.
/// </summary>
/// <param name="Name">What --profile, BIMOPENFLOW_PROFILE, and appsettings.json select it by.</param>
/// <param name="Packs">The registry over a relation runtime.</param>
/// <param name="SeededModelRoots">Folders added to the model roots, from the directory the
/// host runs in; empty outside a checkout.</param>
/// <param name="Seed">Seeds a store with the profile's sample graphs (store, start directory,
/// registry to validate against, log); returns the ids seeded for the first time.</param>
/// <param name="Preparation">Slow generated sample files to build after the port opens.</param>
public sealed record HostProfile(
    string Name,
    Func<RelationRuntime, NodeRegistry> Packs,
    Func<string, IReadOnlyList<string>> SeededModelRoots,
    Func<AnalysisStore, string, INodeRegistry?, TextWriter?, IReadOnlyList<string>> Seed,
    Func<string, IReadOnlyList<SamplePreparation.Job>> Preparation)
{
    /// <summary>The registry over the given relation sources, or over none: enough for
    /// validation, catalogs, and docs.</summary>
    public NodeRegistry Registry(RelationRuntime? relations = null)
        => Packs(relations ?? HostComposition.NoSources());
}

/// <summary>The profiles a host offers, and the one it starts with when none is named.</summary>
public sealed record HostProfiles(string Default, IReadOnlyList<HostProfile> All)
{
    public IReadOnlyList<string> Names
        => All.Select(p => p.Name).ToList();

    /// <summary>The profile with this name; an unknown name throws, listing the allowed ones.</summary>
    public HostProfile this[string name]
        => All.FirstOrDefault(p => p.Name == name)
            ?? throw new ArgumentException(
                $"Invalid profile '{name}'. Allowed values: {string.Join(", ", Names)}.");
}
