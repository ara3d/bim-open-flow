using Ara3D.BimOpenSchema.Federation;
using Ara3D.NodeGraph;
using BimOpenToolkit.TestSupport;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>
/// Builds the pieces the SnowdonFederationGraph tests need: the five-document
/// FederationExample union as a DuckDB file, a confirmations CSV the caller
/// supplies the text for, and a copy of the committed match graph with its
/// three placeholders ({FEDERATION_UNION}, {FEDERATION_CONFIRMATIONS},
/// {FEDERATION_CORRESPONDENCE}) rewritten to point at a temp folder. Never
/// touches the committed samples/snowdon-analyses/federation-confirmations.csv.
/// </summary>
public static class SnowdonFederationFixture
{
    public const string UnionPlaceholder = "{FEDERATION_UNION}";
    public const string ConfirmationsPlaceholder = "{FEDERATION_CONFIRMATIONS}";
    public const string CorrespondencePlaceholder = "{FEDERATION_CORRESPONDENCE}";

    public static string GraphFile
        => RepoPaths.Samples("snowdon-analyses", "federation-match.json");

    public sealed record Built(string Dir, string UnionDuckDb, string Confirmations, string Correspondence, GraphDocument Graph);

    /// <summary>Writes the example union DuckDB and the given confirmations CSV text into
    /// a fresh temp dir under <paramref name="dir"/>, and returns a copy of the committed
    /// graph with its placeholders rewritten to those paths (and a not-yet-written
    /// correspondence.parquet path).</summary>
    public static Built Build(string dir, string confirmationsCsv)
    {
        Directory.CreateDirectory(dir);

        var unionDb = Path.Combine(dir, "union.duckdb");
        BosUnion.WriteDuckDb(FederationExample.Union(), unionDb);

        var confirmations = Path.Combine(dir, "federation-confirmations.csv");
        File.WriteAllText(confirmations, confirmationsCsv);

        var correspondence = Path.Combine(dir, "correspondence.parquet");

        var doc = GraphDocumentIO.Load(GraphFile);
        doc = Rewrite(doc, UnionPlaceholder, unionDb);
        doc = Rewrite(doc, ConfirmationsPlaceholder, confirmations);
        doc = Rewrite(doc, CorrespondencePlaceholder, correspondence);

        return new Built(dir, unionDb, confirmations, correspondence, doc);
    }

    private static GraphDocument Rewrite(GraphDocument doc, string placeholder, string path)
    {
        var target = path.Replace('\\', '/');
        return doc with
        {
            Values = doc.Values.ToDictionary(
                node => node.Key,
                node => (IReadOnlyDictionary<string, string>)node.Value.ToDictionary(
                    p => p.Key,
                    p => p.Value.Replace(placeholder, target))),
        };
    }
}
