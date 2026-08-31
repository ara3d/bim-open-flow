using BimOpenFlow.Mcp;

namespace BimOpenFlow.Mcp.Tests;

public sealed class EditAndEvalToolTests : FlowToolFixture
{
    [Test]
    public void AuthorEvaluateAndReadResult()
    {
        AuthorCameraSort("smoke");

        var state = Json(FlowEvalTools.Evaluate(Services, "smoke"));
        var statuses = state.GetProperty("nodes").EnumerateArray()
            .Select(n => n.GetProperty("status").GetString())
            .ToList();
        Assert.That(statuses, Is.EqualTo(new[] { "Ok", "Ok" }));

        var slice = Json(FlowEvalTools.GetResult(Services, "smoke", "sort", "table", skip: 0, take: 10));
        Assert.Multiple(() =>
        {
            Assert.That(slice.GetProperty("totalRows").GetInt32(), Is.EqualTo(1));
            Assert.That(slice.GetProperty("columns")[0].GetProperty("name").GetString(), Is.EqualTo("name"));
            Assert.That(slice.GetProperty("rows")[0][0].GetString(), Is.EqualTo("front"));
        });
    }

    [Test]
    public void AddNode_UnknownKindIsToolError()
        => Assert.That(
            Assert.Throws<ArgumentException>(() =>
                FlowEditTools.AddNode(Services, "a", "x", "no.such", version: null))!.Message,
            Does.Contain("Unknown node kind"));

    [Test]
    public void Connect_ToMissingNodeFailsValidationAndDoesNotSave()
    {
        FlowEditTools.AddNode(Services, "a", "cam", "view3d.camera", version: null);
        var before = Json(FlowDocumentTools.GetAnalysis(Services, "a")).GetProperty("graphHash").GetString();

        Assert.That(
            Assert.Throws<ArgumentException>(() =>
                FlowEditTools.Connect(Services, "a", "cam.camera", "ghost.table"))!.Message,
            Does.Contain("Invalid graph"));

        var after = Json(FlowDocumentTools.GetAnalysis(Services, "a")).GetProperty("graphHash").GetString();
        Assert.That(after, Is.EqualTo(before));
    }

    [Test]
    public void RemoveNode_DropsEdgesAndParams()
    {
        AuthorCameraSort("smoke");
        FlowEditTools.RemoveNode(Services, "smoke", "sort");
        var json = Json(FlowDocumentTools.GetAnalysis(Services, "smoke")).GetProperty("json").GetString()!;
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("sort"));
            Assert.That(json, Does.Contain("cam"));
        });
    }

    [Test]
    public void Evaluate_UnknownAnalysisIsToolError()
        => Assert.Throws<FileNotFoundException>(() => FlowEvalTools.Evaluate(Services, "nope"));

    [Test]
    public void CreateRun_ThenListRuns()
    {
        AuthorCameraSort("smoke");
        var created = Json(FlowEvalTools.CreateRun(Services, "smoke"));
        Assert.That(created.GetProperty("fileName").GetString(), Does.EndWith(".run.json"));

        var runs = Json(FlowEvalTools.ListRuns(Services, "smoke"));
        Assert.That(runs.EnumerateArray().Single().GetProperty("graphHash").GetString(),
            Is.EqualTo(created.GetProperty("graphHash").GetString()));
    }
}
