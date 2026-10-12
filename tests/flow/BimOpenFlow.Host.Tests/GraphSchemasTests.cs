using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.NodeGraph;
using BimOpenFlow.Contracts;
using BimOpenFlow.Host.Api;
using DuckDB.NET.Data;
using ColumnType = Ara3D.DataFlowEngine.Abstractions.ColumnType;

namespace BimOpenFlow.Host.Tests;

/// <summary>The static schema pass (TKT-209) over the real "tables" profile registry and the catalog the
/// host composes: nothing here evaluates a node.</summary>
public sealed class GraphSchemasTests
{
    private const string Rows = """[{"Name":"a","Qty":1},{"Name":"b","Qty":2}]""";

    private static readonly NodeRegistry Registry = HostComposition.TablePacks();
    private static readonly ISchemaCatalog Catalog = HostComposition.SchemaCatalog(HostComposition.NoSources());

    private string _dir = "";

    [SetUp]
    public void MakeDir()
        => _dir = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "bof-schemas-" + Guid.NewGuid().ToString("N"))).FullName;

    [TearDown]
    public void RemoveDir()
        => Directory.Delete(_dir, recursive: true);

    private static string Json(string text)
        => text.Replace("'", "\"");

    private static GraphDocument Doc(string nodes, string edges, string values, string inputs = "")
        => GraphDocumentIO.Parse(Json(
            "{'formatVersion':'0.1.0','layout':{},'structure':{'edges':[" + edges + "],'nodes':[" + nodes + "]" + inputs
            + "},'values':{" + values + "}}"));

    private static string Node(string id, string kind)
        => $"{{'id':'{id}','kind':'{kind}','version':1}}";

    private static string Edge(string from, string to)
        => $"{{'from':'{from}','to':'{to}'}}";

    private static string Inline(string id = "src", string rows = Rows)
        => $"'{id}':{{'rows':{System.Text.Json.JsonSerializer.Serialize(rows)}}}";

    private static IReadOnlyList<InferredPort> Infer(GraphDocument doc, IReadOnlyDictionary<string, string>? inputs = null)
        => GraphSchemas.Infer(doc, Registry, Catalog, inputs);

    private static InferredPort Port(IReadOnlyList<InferredPort> ports, string node)
        => ports.Single(p => p.Node == node);

    private static string[] Names(InferredPort port)
        => port.Schema!.Columns.Select(c => c.Name).ToArray();

    [Test]
    public void AStaticNodeGivesItsDeclaredColumns()
    {
        var ports = Infer(Doc($"{Node("src", "table.inline")},{Node("prof", "table.profile")}",
            Edge("src.table", "prof.table"), Inline()));
        var declared = Registry.Find("table.profile", 1)!.Spec.Schemas.OfType<StaticSchemaSpec>().Single();
        Assert.That(Port(ports, "prof").Schema, Is.EqualTo(declared.Schema));
    }

    [Test]
    public void AnInferredChainGivesTheColumnsTheRunWould()
    {
        var ports = Infer(Doc($"{Node("src", "table.inline")},{Node("keep", "table.project")}",
            Edge("src.table", "keep.table"), Inline() + ",'keep':{'columns':'Qty'}"));
        Assert.Multiple(() =>
        {
            Assert.That(Names(Port(ports, "src")), Is.EqualTo(new[] { "Name", "Qty" }));
            Assert.That(Port(ports, "src").Schema!.Find("Qty")!.Value.Type, Is.EqualTo(ColumnType.Integer));
            Assert.That(Names(Port(ports, "keep")), Is.EqualTo(new[] { "Qty" }));
        });
    }

    [Test]
    public void ADynamicNodeBreaksTheChainAndIsNamedInTheReason()
    {
        var ports = Infer(Doc(
            $"{Node("src", "table.inline")},{Node("pv", "table.pivot")},{Node("keep", "table.project")}",
            $"{Edge("src.table", "pv.table")},{Edge("pv.table", "keep.table")}",
            Inline() + ",'pv':{'groupBy':'Name','nameColumn':'Name','valueColumn':'Qty'},'keep':{'columns':'Name'}"));
        Assert.Multiple(() =>
        {
            Assert.That(Port(ports, "pv").Status, Is.EqualTo(SchemaStatus.Dynamic));
            Assert.That(Port(ports, "keep").Status, Is.EqualTo(SchemaStatus.Unknown));
            Assert.That(Port(ports, "keep").Reason, Does.Contain("'pv' (table.pivot) is dynamic"));
        });
    }

    [Test]
    public void AnUpstreamFailureIsReportedOnceAndNamedDownstream()
    {
        var ports = Infer(Doc($"{Node("src", "table.inline")},{Node("keep", "table.project")}",
            Edge("src.table", "keep.table"), "'src':{'rows':'not json'},'keep':{'columns':'Name'}"));
        Assert.Multiple(() =>
        {
            Assert.That(Port(ports, "src").Status, Is.EqualTo(SchemaStatus.Error));
            Assert.That(Port(ports, "src").Errors, Is.Not.Empty);
            Assert.That(Port(ports, "keep").Status, Is.EqualTo(SchemaStatus.Unknown));
            Assert.That(Port(ports, "keep").Reason, Does.Contain("'src' (table.inline) failed"));
            Assert.That(Port(ports, "keep").Errors, Is.Null.Or.Empty);
        });
    }

    [Test]
    public void AMissingColumnWarnsOnTheNodeThatNamesItWithTheNearestMatch()
    {
        var ports = Infer(Doc($"{Node("src", "table.inline")},{Node("keep", "table.project")}",
            Edge("src.table", "keep.table"), Inline() + ",'keep':{'columns':'Nmae'}"));
        Assert.Multiple(() =>
        {
            Assert.That(Port(ports, "src").Warnings, Is.Empty);
            Assert.That(Port(ports, "keep").Status, Is.EqualTo(SchemaStatus.Ok));
            Assert.That(Port(ports, "keep").Warnings, Has.Count.EqualTo(1));
            Assert.That(Port(ports, "keep").Warnings![0], Does.Contain("Nmae").And.Contain("did you mean 'Name'"));
        });
    }

    [Test]
    public void ADatabaseWiredFromDuckSourceReachesDuckTable()
    {
        var db = MakeDatabase();
        var ports = Infer(Doc($"{Node("db", "duck.source")},{Node("orders", "duck.table")}",
            Edge("db.source", "orders.path"), $"'db':{{'path':{System.Text.Json.JsonSerializer.Serialize(db)}}},'orders':{{'table':'Orders'}}"));
        Assert.That(Names(Port(ports, "orders")), Is.EqualTo(new[] { "OrderId", "Customer" }));
    }

    [Test]
    public void AGraphInputPathReachesDuckTableAndFollowsTheInputsValue()
    {
        var db = MakeDatabase();
        var doc = Doc(Node("orders", "duck.table"), "", "'orders':{'path':'$db','table':'Orders'}",
            ",'inputs':[{'name':'db','kind':'FilePath','default':''}]");
        Assert.Multiple(() =>
        {
            Assert.That(Names(Port(Infer(doc, new Dictionary<string, string> { ["db"] = db }), "orders")),
                Is.EqualTo(new[] { "OrderId", "Customer" }));
            Assert.That(Port(Infer(doc), "orders").Status, Is.EqualTo(SchemaStatus.Error), "the default path is empty");
        });
    }

    [Test]
    public void ADocumentThatDoesNotValidateHasNoSchemas()
        => Assert.That(Infer(Doc(Node("x", "no.such.kind"), "", "")), Is.Empty);

    private string MakeDatabase()
    {
        var path = Path.Combine(_dir, "orders.duckdb");
        using var connection = new DuckDBConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Orders (OrderId INTEGER, Customer VARCHAR); INSERT INTO Orders VALUES (1, 'a')";
        command.ExecuteNonQuery();
        return path;
    }
}
