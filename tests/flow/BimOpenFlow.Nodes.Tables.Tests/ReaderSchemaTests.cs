using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using BimOpenFlow.Relations;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace BimOpenFlow.Nodes.Tables.Tests;

/// <summary>The file readers of the Tables pack: the schema each infers (or declares) equals the columns
/// of the table it evaluates to, on a workbook, a SQLite database and a BFAST file written into a temp folder.</summary>
[TestFixture]
public sealed class ReaderSchemaTests
{
    private string _dir = "";
    private string _xlsx = "", _db = "", _bfast = "";
    private readonly ISchemaCatalog _catalog = new TablesCatalog(new StaticCatalog(new Dictionary<string, Schema>()));

    [OneTimeSetUp]
    public void WriteFiles()
    {
        _dir = Directory.CreateTempSubdirectory("tables-reader-schemas-").FullName;
        _xlsx = Path.Combine(_dir, "book.xlsx");
        using (var workbook = new XLWorkbook())
        {
            var people = workbook.AddWorksheet("People");
            people.Cell(1, 1).Value = "Name";
            people.Cell(1, 2).Value = "Age";
            people.Cell(1, 3).Value = "Active";
            people.Cell(1, 4).Value = "Born";
            people.Cell(2, 1).Value = "Alice";
            people.Cell(2, 2).Value = 30.0;
            people.Cell(2, 3).Value = true;
            people.Cell(2, 4).Value = new DateTime(1995, 6, 1);
            var mixed = workbook.AddWorksheet("Mixed");
            mixed.Cell(1, 1).Value = "V";
            mixed.Cell(2, 1).Value = 1.5;
            mixed.Cell(3, 1).Value = "text";
            var junk = workbook.AddWorksheet("Junk");
            junk.Cell(1, 1).Value = "Quarterly Report";
            junk.Cell(3, 1).Value = "Item";
            junk.Cell(3, 2).Value = "Count";
            junk.Cell(4, 1).Value = "bolt";
            junk.Cell(4, 2).Value = 10.0;
            workbook.SaveAs(_xlsx);
        }

        _db = Path.Combine(_dir, "test.db");
        using (var connection = new SqliteConnection($"Data Source={_db}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE items (name TEXT, qty INTEGER, price REAL); INSERT INTO items VALUES ('bolt', 10, 0.25);";
            command.ExecuteNonQuery();
        }

        _bfast = Path.Combine(_dir, "sample.bfast");
        SampleBfast.Write(_bfast);
    }

    [OneTimeTearDown]
    public void DeleteFiles()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private SchemaResult Infer(IFlowNode node, params (string Name, string Value)[] ps)
        => ((ISchemaNode)node).Infer(node.Spec.Schemas[0].Output, [], NodeTestHelpers.Params(ps), _catalog);

    private static string Describe(Schema schema) => string.Join(", ", schema.Columns.Select(c => $"{c.Name}:{c.Type}"));

    private void AssertMatchesEvaluation(IFlowNode node, params (string Name, string Value)[] ps)
    {
        var inferred = Infer(node, ps);
        Assert.That(inferred.Ok, Is.True, inferred.ToString());
        Assert.That(Describe(inferred.Schema!), Is.EqualTo(Describe(TableSchemas.Of(node.EvalTable([], ps)))));
    }

    private static void AssertStaticMatchesEvaluation(IFlowNode node, params (string Name, string Value)[] ps)
    {
        var declared = ((StaticSchemaSpec)node.Spec.Schemas.Single()).Schema;
        Assert.That(Describe(declared), Is.EqualTo(Describe(TableSchemas.Of(node.EvalTable([], ps)))));
    }

    [TestCase("People")]
    [TestCase("Mixed")]
    [TestCase("")]
    public void XlsxRead_matches_evaluation(string sheet)
        => AssertMatchesEvaluation(new XlsxReadNode(), ("path", _xlsx), ("sheet", sheet));

    [Test]
    public void XlsxRead_header_row_and_range_match_evaluation()
    {
        AssertMatchesEvaluation(new XlsxReadNode(), ("path", _xlsx), ("sheet", "Junk"), ("headerRow", "3"));
        AssertMatchesEvaluation(new XlsxReadNode(), ("path", _xlsx), ("sheet", "Junk"), ("range", "A3:B4"));
    }

    [Test]
    public void XlsxRead_bad_sheet_header_row_or_range_is_a_failed_result()
    {
        Assert.That(Infer(new XlsxReadNode(), ("path", _xlsx), ("sheet", "Nope")).ToString(), Does.Contain("Nope"));
        Assert.That(Infer(new XlsxReadNode(), ("path", _xlsx), ("headerRow", "0")).ToString(), Does.Contain("headerRow"));
        Assert.That(Infer(new XlsxReadNode(), ("path", _xlsx), ("range", "not a range")).Ok, Is.False);
    }

    [Test]
    public void XlsxSheets_SqliteTables_and_BfastRead_declare_what_they_evaluate()
    {
        AssertStaticMatchesEvaluation(new XlsxSheetsNode(), ("path", _xlsx));
        AssertStaticMatchesEvaluation(new SqliteTablesNode(), ("path", _db));
        AssertStaticMatchesEvaluation(new BfastReadNode(), ("path", _bfast));
    }

    [TestCase("uint8")]
    [TestCase("int16")]
    [TestCase("int32")]
    [TestCase("int64")]
    [TestCase("float32")]
    [TestCase("float64")]
    public void BfastBuffer_type_decides_the_value_column(string type)
    {
        var inferred = Infer(new BfastBufferNode(), ("path", ""), ("name", "orderIds"), ("type", type));
        Assert.That(inferred.Ok, Is.True, inferred.ToString());
        AssertMatchesEvaluation(new BfastBufferNode(), ("path", _bfast), ("name", "orderIds"), ("type", type));
    }

    [Test]
    public void BfastBuffer_unknown_type_is_a_failed_result()
        => Assert.That(Infer(new BfastBufferNode(), ("type", "float128")).ToString(), Does.Contain("float128"));

    [Test]
    public void SqliteTable_and_query_know_the_names_and_not_the_types()
    {
        var table = Infer(new SqliteTableNode(), ("path", _db), ("table", "items"));
        Assert.That(Describe(table.Require()), Is.EqualTo("name:Unknown, qty:Unknown, price:Unknown"));
        Assert.That(table.Schema!.Columns.Select(c => c.Name),
            Is.EqualTo(new SqliteTableNode().EvalTable([], ("path", _db), ("table", "items")).ColumnNames()));

        var sql = "SELECT qty * 2 AS twice, name FROM items WHERE qty > 1 ORDER BY qty;";
        var query = Infer(new SqliteQueryNode(), ("path", _db), ("sql", sql));
        Assert.That(query.Schema!.Columns.Select(c => c.Name),
            Is.EqualTo(new SqliteQueryNode().EvalTable([], ("path", _db), ("sql", sql)).ColumnNames()));
    }

    [Test]
    public void SqliteTable_and_query_with_a_bad_table_or_statement_are_failed_results()
    {
        Assert.That(Infer(new SqliteTableNode(), ("path", _db), ("table", "floors")).ToString(), Does.Contain("floors"));
        Assert.That(Infer(new SqliteQueryNode(), ("path", _db), ("sql", "SELECT nothing FROM items")).Ok, Is.False);
        Assert.That(Infer(new SqliteQueryNode(), ("path", _db), ("sql", "DELETE FROM items")).ToString(), Does.Contain("sqlite.query"));
    }

    [TestCase("xlsx.read")]
    [TestCase("sqlite.table")]
    [TestCase("sqlite.query")]
    public void Readers_with_a_missing_file_an_empty_path_or_a_graph_input_give_unknown_with_a_message(string kind)
    {
        IFlowNode node = new IFlowNode[] { new XlsxReadNode(), new SqliteTableNode(), new SqliteQueryNode() }.Single(n => n.Spec.Kind == kind);
        foreach (var path in new[] { Path.Combine(_dir, "absent"), "", "$source" })
        {
            var result = Infer(node, ("path", path), ("table", "items"), ("sql", "SELECT 1"));
            Assert.That(result.Ok, Is.False, path);
            Assert.That(result.ToString(), Does.Contain(kind), path);
        }
    }

    [Test]
    public void Formats_the_pack_does_not_own_go_to_the_wrapped_catalog()
        => Assert.That(_catalog.FileSchema("duckdb", "a.duckdb", new Dictionary<string, string>()).ToString(), Does.Contain("duckdb"));

    [Test]
    public void Reader_specs_pass_the_schema_declaration_checks()
    {
        IFlowNode[] readers = [new XlsxReadNode(), new XlsxSheetsNode(), new SqliteQueryNode(), new SqliteTableNode(),
            new SqliteTablesNode(), new BfastReadNode(), new BfastBufferNode()];
        Assert.That(readers.SelectMany(n => n.SchemaProblems()), Is.Empty);
        Assert.That(readers.Select(n => n.Spec.Schemas.Count), Is.All.EqualTo(1));
    }
}
