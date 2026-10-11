using Ara3D.DataFlowEngine.Runs;
using BimOpenFlow.Host.Store;
using BimOpenMcp.Flow;

namespace BimOpenMcp.Flow.Tests;

/// <summary>An agent declares a graph input, refers to it from a node, and swaps its value with
/// one setParam; evaluate and createRun take values for it; getAnalysis reports it.</summary>
public sealed class GraphInputToolTests : FlowToolFixture
{
    private const string Id = "inputs";

    private void Author()
        => FlowEditTools.EditGraph(Services, Id, """
            [
              {"op":"declareInput","name":"rows","kind":"Text","value":"[{\"name\":\"front\"}]","description":"The rows to sort"},
              {"op":"addNode","nodeId":"data","kind":"table.inline"},
              {"op":"setParam","nodeId":"data","name":"rows","value":"$rows"},
              {"op":"addNode","nodeId":"sort","kind":"table.sort"},
              {"op":"setParam","nodeId":"sort","name":"by","value":"name"},
              {"op":"connect","from":"data.table","to":"sort.table"}
            ]
            """);

    private string FirstName()
        => Json(FlowEvalTools.GetResult(Services, Id, "sort", "table", skip: 0, take: 10))
            .GetProperty("rows")[0][0].GetString()!;

    [Test]
    public void GetAnalysis_ReportsDeclarationAndCurrentValue()
    {
        Author();
        var input = Json(FlowDocumentTools.GetAnalysis(Services, Id)).GetProperty("inputs")[0];
        Assert.Multiple(() =>
        {
            Assert.That(input.GetProperty("name").GetString(), Is.EqualTo("rows"));
            Assert.That(input.GetProperty("kind").GetString(), Is.EqualTo("Text"));
            Assert.That(input.GetProperty("description").GetString(), Is.EqualTo("The rows to sort"));
            Assert.That(input.GetProperty("value").GetString(), Is.EqualTo(input.GetProperty("default").GetString()));
        });
        Assert.That(FirstName(), Is.EqualTo("front"));
    }

    [Test]
    public void SetParam_OnADollarName_SwapsTheValueAndEveryReaderFollows()
    {
        Author();
        var hash = Json(FlowDocumentTools.GetAnalysis(Services, Id)).GetProperty("graphHash").GetString();

        var result = Json(FlowEditTools.SetGraphInput(Services, Id, "$rows", """[{"name":"back"}]"""));
        Assert.That(result.GetProperty("value").GetString(), Is.EqualTo("""[{"name":"back"}]"""));
        Assert.That(result.GetProperty("graphHash").GetString(), Is.EqualTo(hash), "the document did not change");
        Assert.That(FirstName(), Is.EqualTo("back"));

        var input = Json(FlowDocumentTools.GetAnalysis(Services, Id)).GetProperty("inputs")[0];
        Assert.That(input.GetProperty("value").GetString(), Is.EqualTo("""[{"name":"back"}]"""));
        Assert.That(input.GetProperty("default").GetString(), Is.EqualTo("""[{"name":"front"}]"""));
    }

    [Test]
    public void SetParam_NameWithoutDollarAndNoNode_ExplainsWhatToWrite()
    {
        Author();
        Assert.That(Assert.Throws<ArgumentException>(
                () => FlowEditTools.SetGraphInput(Services, Id, "rows", "x"))!.Message,
            Does.Contain("'$rows'"));
    }

    [Test]
    public void SetParam_UnknownInput_NamesIt()
    {
        Author();
        Assert.That(Assert.Throws<Ara3D.DataFlowEngine.InvalidGraphException>(
                () => FlowEditTools.SetGraphInput(Services, Id, "$nope", "x"))!.Message,
            Does.Contain("'nope'"));
    }

    [Test]
    public void Evaluate_WithGraphInputs_UsesAndKeepsThem()
    {
        Author();
        var state = Json(FlowEvalTools.Evaluate(Services, Id,
            new Dictionary<string, string> { ["rows"] = """[{"name":"mid"}]""" }));
        Assert.That(state.GetProperty("graphInputs")[0].GetProperty("value").GetString(), Is.EqualTo("""[{"name":"mid"}]"""));
        Assert.That(FirstName(), Is.EqualTo("mid"));
        Assert.That(Json(FlowEvalTools.Evaluate(Services, Id)).GetProperty("graphInputs")[0].GetProperty("value").GetString(),
            Is.EqualTo("""[{"name":"mid"}]"""), "values stay until changed");
    }

    [Test]
    public void CreateRun_PinsTheGraphInputValue()
    {
        Author();
        var summary = Json(FlowEvalTools.CreateRun(Services, Id,
            new Dictionary<string, string> { ["rows"] = """[{"name":"run"}]""" }));
        var record = Services.Host.Store.LoadRun(Id, summary.GetProperty("fileName").GetString()!);
        Assert.That(record.GraphInputs["rows"], Is.EqualTo("""[{"name":"run"}]"""));
    }

    [Test]
    public void EditGraph_InvalidDeclarationSavesNothing()
    {
        Assert.That(Assert.Throws<ArgumentException>(() => FlowEditTools.EditGraph(Services, Id, """
                [{"op":"declareInput","name":"n","kind":"Integer","value":"abc"}]
                """))!.Message,
            Does.Contain("'n'"));
        Assert.That(Services.Host.Store.Exists(Id), Is.False);
    }

    [Test]
    public void EditGraph_RemovingADeclarationStillReferencedFailsValidation()
    {
        Author();
        Assert.That(Assert.Throws<ArgumentException>(() => FlowEditTools.EditGraph(Services, Id, """
                [{"op":"removeInput","name":"rows"}]
                """))!.Message,
            Does.Contain("rows"), "removing a declaration that nodes still refer to fails validation");
    }
}
