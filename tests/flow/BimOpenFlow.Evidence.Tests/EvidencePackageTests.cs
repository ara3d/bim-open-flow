using System.IO.Compression;
using System.Text;
using Ara3D.DataFlowEngine.Runs;
using Ara3D.NodeGraph;

namespace BimOpenFlow.Evidence.Tests;

public class EvidencePackageTests
{
    private const string Created = "2026-08-31T12:00:00.000Z";
    private static readonly string OutputHash = new('b', 64);

    private string _dir = "";

    [SetUp]
    public void SetUp()
        => _dir = Directory.CreateTempSubdirectory("bof-evidence").FullName;

    [TearDown]
    public void TearDown()
        => Directory.Delete(_dir, recursive: true);

    private static GraphDocument Graph()
        => GraphDocument.Empty with
        {
            Nodes = new[] { new GraphNode("n1", "source.model", 1) },
        };

    private static RunRecord Run()
        => RunRecordJson.Parse($$"""
            {
              "runVersion": "0.1.0",
              "graphHash": "{{Graph().ComputeGraphHash()}}",
              "engineVersion": "1.0.0",
              "timestampUtc": "2026-08-31T00:00:00.000Z",
              "inputs": [],
              "nodeOutputs": { "n1.out": "{{OutputHash}}" },
              "recordedOutputs": { "n1.out": { "kind": "Integer", "value": 7 } },
              "effects": []
            }
            """);

    private string BuildPackage(out EvidenceManifest manifest)
    {
        var path = Path.Combine(_dir, "package.zip");
        manifest = EvidencePackage.Build(
            Graph(), Run(), "<!doctype html>\n<p>report</p>",
            new[] { new EvidenceInput("model.ifc", Encoding.UTF8.GetBytes("IFC-DATA")) },
            Created, path);
        return path;
    }

    [Test]
    public void Build_ThenVerify_Ok()
    {
        var path = BuildPackage(out var manifest);
        Assert.That(manifest.Files.Keys, Is.EquivalentTo(new[]
        {
            "graph.dfg.json", "run.run.json", "report.html", "inputs/model.ifc",
        }));
        Assert.That(manifest.GraphHash, Is.EqualTo(Graph().ComputeGraphHash()));
        Assert.That(manifest.Created, Is.EqualTo(Created));

        var result = EvidencePackage.Verify(path);
        Assert.That(result.Mismatches, Is.Empty);
        Assert.That(result.Ok, Is.True);
    }

    [Test]
    public void Verify_TamperedMemberReported()
    {
        var path = BuildPackage(out _);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            zip.GetEntry("report.html")!.Delete();
            var entry = zip.CreateEntry("report.html");
            using var w = new StreamWriter(entry.Open());
            w.Write("<p>tampered</p>");
        }
        var result = EvidencePackage.Verify(path);
        Assert.That(result.Ok, Is.False);
        Assert.That(result.Mismatches, Has.Exactly(1).Contains("hash mismatch for 'report.html'"));
    }

    [Test]
    public void Verify_MissingAndUnlistedMembersReported()
    {
        var path = BuildPackage(out _);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            zip.GetEntry("inputs/model.ifc")!.Delete();
            var extra = zip.CreateEntry("extra.txt");
            using var w = new StreamWriter(extra.Open());
            w.Write("surprise");
        }
        var result = EvidencePackage.Verify(path);
        Assert.That(result.Ok, Is.False);
        Assert.That(result.Mismatches, Has.Exactly(1).Contains("missing member 'inputs/model.ifc'"));
        Assert.That(result.Mismatches, Has.Exactly(1).Contains("unlisted member 'extra.txt'"));
    }

    [Test]
    public void Build_GraphRunHashMismatchThrows()
    {
        var otherGraph = GraphDocument.Empty with
        {
            Nodes = new[] { new GraphNode("other", "source.model", 1) },
        };
        Assert.Throws<ArgumentException>(() => EvidencePackage.Build(
            otherGraph, Run(), "<p>r</p>", Array.Empty<EvidenceInput>(),
            Created, Path.Combine(_dir, "bad.zip")));
    }

    [Test]
    public void Build_InvalidTimestampThrows()
        => Assert.Throws<ArgumentException>(() => EvidencePackage.Build(
            Graph(), Run(), "<p>r</p>", Array.Empty<EvidenceInput>(),
            "2026-08-31", Path.Combine(_dir, "bad.zip")));

    [Test]
    public void EvidenceInput_RejectsPathLikeNames()
    {
        Assert.Throws<ArgumentException>(() => new EvidenceInput("a/b.ifc", Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => new EvidenceInput("..", Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => new EvidenceInput("", Array.Empty<byte>()));
    }

    [Test]
    public void Manifest_CanonicalJsonRoundTrips()
    {
        BuildPackage(out var manifest);
        var parsed = EvidenceManifest.Parse(manifest.ToCanonicalJson());
        Assert.That(parsed.ToCanonicalJson(), Is.EqualTo(manifest.ToCanonicalJson()));
        Assert.That(parsed.PackageVersion, Is.EqualTo("0.1.0"));
        Assert.That(parsed.RunFile, Is.EqualTo("run.run.json"));
    }
}
