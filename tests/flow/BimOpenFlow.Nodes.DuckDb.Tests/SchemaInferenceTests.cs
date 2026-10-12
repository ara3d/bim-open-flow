using Ara3D.BimOpenSchema.DuckDb;
using Ara3D.DataFlowEngine.Abstractions;
using Ara3D.DataTable;
using Ara3D.Utils;
using BimOpenFlow.Relations.DuckDb;

namespace BimOpenFlow.Nodes.DuckDb.Tests;

/// <summary>Every DuckDB reader and query node: the schema it infers equals the columns of the table it
/// evaluates to (names, order, types), on fixture files written by DuckDB itself.</summary>
[TestFixture]
public sealed class SchemaInferenceTests
{
    private const string Rows = "SELECT i AS id, i * 1.5 AS price, 'n' || i AS name, DATE '2024-01-01' + CAST(i AS INTEGER) AS built, "
        + "TIMESTAMP '2024-01-01 10:00:00' AS stamp, i % 2 = 0 AS ok, CAST(i AS DECIMAL(10,2)) AS amount, CAST(i AS HUGEINT) AS big FROM range(1, 4) t(i)";

    private string _folder = null!;
    private string _csv = null!, _parquet = null!, _json = null!, _db = null!;
    private ISchemaCatalog _catalog = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "bimopenflow-duckdb-schema-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
        _csv = Path.Combine(_folder, "rows.csv");
        _parquet = Path.Combine(_folder, "rows.parquet");
        _json = Path.Combine(_folder, "rows.json");
        _db = Path.Combine(_folder, "rows.duckdb");
        using (var conn = BosDuckDb.Open(new FilePath(_db)))
        {
            conn.Execute($"CREATE TABLE items AS {Rows}");
            conn.Execute($"COPY items TO '{_csv.ToSqlLiteral()}' (HEADER)");
            conn.Execute($"COPY items TO '{_parquet.ToSqlLiteral()}' (FORMAT PARQUET)");
            conn.Execute($"COPY (SELECT id, name, ok FROM items) TO '{_json.ToSqlLiteral()}' (FORMAT JSON, ARRAY true)");
        }
        _catalog = new CachedCatalog(new DuckDbCatalog(ConnectionRegistry.Of()));
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { }
    }

    private SchemaResult Infer(IFlowNode node, params (string Name, string Value)[] ps)
        => ((ISchemaNode)node).Infer("table", [], NodeTestHelpers.Params(ps), _catalog);

    private static IReadOnlyList<(string, ColumnType)> Shape(Schema schema)
        => schema.Columns.Select(c => (c.Name, c.Type)).ToList();

    private static IReadOnlyList<(string, ColumnType)> Shape(IDataTable table)
        => table.Columns.Select(c => (c.Descriptor.Name, DuckDbTypes.FromClr(c.Descriptor.Type))).ToList();

    private void AssertMatchesEvaluation(IFlowNode node, params (string Name, string Value)[] ps)
    {
        var inferred = Infer(node, ps);
        Assert.That(inferred.Ok, Is.True, inferred.ToString());
        Assert.That(Shape(inferred.Schema!), Is.EqualTo(Shape(node.EvalTable([], ps))));
    }

    [Test]
    public void CsvRead_matches_evaluation() => AssertMatchesEvaluation(new CsvReadNode(), ("path", _csv));

    [Test]
    public void CsvRead_without_type_inference_is_all_text()
    {
        AssertMatchesEvaluation(new CsvReadNode(), ("path", _csv), ("inferTypes", "false"));
        Assert.That(Infer(new CsvReadNode(), ("path", _csv), ("inferTypes", "false")).Schema!.Columns.Select(c => c.Type),
            Is.All.EqualTo(ColumnType.Text));
    }

    [Test]
    public void CsvRead_without_header_names_columns_Column1_up()
    {
        AssertMatchesEvaluation(new CsvReadNode(), ("path", _csv), ("header", "false"));
        Assert.That(Infer(new CsvReadNode(), ("path", _csv), ("header", "false")).Schema!.Columns[0].Name, Is.EqualTo("Column1"));
    }

    [Test]
    public void CsvRead_glob_adds_filename()
    {
        AssertMatchesEvaluation(new CsvReadNode(), ("path", Path.Combine(_folder, "*.csv")));
        Assert.That(Infer(new CsvReadNode(), ("path", Path.Combine(_folder, "*.csv"))).Schema!.Has("filename"), Is.True);
    }

    [Test]
    public void CsvRead_unknown_encoding_is_a_failed_result()
    {
        var result = Infer(new CsvReadNode(), ("path", _csv), ("encoding", "latin1x"));
        Assert.That(result.Ok, Is.False);
    }

    [Test]
    public void ParquetRead_matches_evaluation_and_turns_dates_into_text()
    {
        AssertMatchesEvaluation(new ParquetReadNode(), ("path", _parquet));
        var built = Infer(new ParquetReadNode(), ("path", _parquet)).Schema!.Find("built")!.Value;
        Assert.That(built.Type, Is.EqualTo(ColumnType.Text));
    }

    [Test]
    public void JsonRead_matches_evaluation() => AssertMatchesEvaluation(new JsonReadNode(), ("path", _json));

    [Test]
    public void JsonRead_flatten_is_unknown_with_a_message()
    {
        var result = Infer(new JsonReadNode(), ("path", _json), ("flatten", "true"));
        Assert.That(result.Ok, Is.False);
        Assert.That(result.ToString(), Does.Contain("json.read").And.Contain("flatten"));
    }

    [TestCase("rows.csv")]
    [TestCase("rows.parquet")]
    [TestCase("rows.json")]
    public void DuckRead_matches_evaluation(string file)
        => AssertMatchesEvaluation(new DuckReadNode(), ("path", Path.Combine(_folder, file)));

    [Test]
    public void DuckRead_unknown_extension_is_a_failed_result()
    {
        var other = Path.Combine(_folder, "rows.dat");
        File.WriteAllText(other, "x");
        var result = Infer(new DuckReadNode(), ("path", other));
        Assert.That(result.ToString(), Does.Contain("duck.read").And.Contain("format"));
    }

    [Test]
    public void DuckTable_matches_evaluation() => AssertMatchesEvaluation(new DuckTableNode(), ("path", _db), ("table", "items"));

    [Test]
    public void DuckTable_from_a_wired_database_reads_the_database_path()
    {
        var source = new DatabaseValue(_db, "hash");
        var parameters = new ParamValues(new Dictionary<string, string> { ["table"] = "items" },
            new Dictionary<string, FlowValue> { ["path"] = source });
        var inferred = new DuckTableNode().Infer("table", [], parameters, _catalog);
        Assert.That(Shape(inferred.Require()), Is.EqualTo(Shape(new DuckTableNode().EvalTable(source, ("table", "items")))));
    }

    [Test]
    public void DuckTable_unknown_table_names_it()
        => Assert.That(Infer(new DuckTableNode(), ("path", _db), ("table", "floors")).ToString(), Does.Contain("floors"));

    [Test]
    public void DuckTable_without_a_table_name_is_unknown()
        => Assert.That(Infer(new DuckTableNode(), ("path", _db)).Ok, Is.False);

    [Test]
    public void DuckQuery_matches_evaluation()
        => AssertMatchesEvaluation(new DuckQueryNode(), ("path", _db), ("sql", "SELECT id, price, built, count(*) OVER () AS n FROM items"));

    [Test]
    public void DuckQuery_from_a_wired_database_reads_the_database_path()
    {
        var source = new DatabaseValue(_db, "hash");
        var sql = "SELECT name, ok FROM items";
        var parameters = new ParamValues(new Dictionary<string, string> { ["sql"] = sql },
            new Dictionary<string, FlowValue> { ["path"] = source });
        Assert.That(Shape(new DuckQueryNode().Infer("table", [], parameters, _catalog).Require()),
            Is.EqualTo(Shape(new DuckQueryNode().EvalTable(source, ("sql", sql)))));
    }

    [Test]
    public void DuckQuery_bad_sql_is_a_failed_result()
    {
        Assert.That(Infer(new DuckQueryNode(), ("path", _db), ("sql", "SELECT nothing FROM items")).Ok, Is.False);
        Assert.That(Infer(new DuckQueryNode(), ("path", _db), ("sql", "DROP TABLE items")).ToString(), Does.Contain("duck.query"));
    }

    [Test]
    public void DuckTables_declares_the_columns_it_evaluates()
    {
        var spec = (StaticSchemaSpec)new DuckTablesNode().Spec.Schemas.Single();
        Assert.That(Shape(spec.Schema), Is.EqualTo(Shape(new DuckTablesNode().EvalTable([], ("path", _db)))));
    }

    [Test]
    public void DuckSource_has_no_table_schema()
        => Assert.That(new DuckSourceNode().Spec.Schemas, Is.Empty);

    private static Schema Of(IDataTable table) => new(Shape(table).Select(c => new Column(c.Item1, c.Item2)).ToList());

    private static IReadOnlyList<SchemaInput> Wired(params Schema?[] schemas)
        => Enumerable.Range(0, 4)
            .Select(i => i < schemas.Length && schemas[i] is { } s
                ? new SchemaInput(PortType.Table, true, SchemaResult.Of(s))
                : new SchemaInput(PortType.Table, false))
            .ToList();

    private static IReadOnlyList<FlowValue> Tables(params IDataTable?[] tables)
        => Enumerable.Range(0, 4)
            .Select(i => i < tables.Length && tables[i] != null ? (FlowValue)new TableValue(tables[i]!) : MissingValue.Instance)
            .ToList();

    private IDataTable Customers() => new CsvReadNode().EvalTable([], ("path", NodeTestHelpers.SamplePath("customers.csv")));
    private IDataTable Orders() => new CsvReadNode().EvalTable([], ("path", NodeTestHelpers.SamplePath("orders.csv")));

    [TestCase("SELECT City, count(*) AS n FROM t GROUP BY City")]
    [TestCase("WITH x AS (SELECT * FROM t1) SELECT * FROM x")]
    [TestCase("with recursive r(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM r WHERE n < 3) SELECT n FROM r")]
    public void SqlQuery_one_input_matches_evaluation(string sql)
    {
        var customers = Customers();
        var inferred = new SqlQueryNode().Infer("table", Wired(Of(customers)), NodeTestHelpers.Params(("sql", sql)), _catalog);
        var evaluated = new SqlQueryNode().EvalTable(Tables(customers), ("sql", sql));
        Assert.That(Shape(inferred.Require()), Is.EqualTo(Shape(evaluated)));
    }

    [Test]
    public void SqlQuery_two_inputs_matches_evaluation()
    {
        var (orders, customers) = (Orders(), Customers());
        var sql = "SELECT t2.Name, count(*) AS Orders FROM t1 JOIN t2 USING (CustomerId) GROUP BY t2.Name";
        var inferred = new SqlQueryNode().Infer("table", Wired(Of(orders), Of(customers)), NodeTestHelpers.Params(("sql", sql)), _catalog);
        Assert.That(Shape(inferred.Require()), Is.EqualTo(Shape(new SqlQueryNode().EvalTable(Tables(orders, customers), ("sql", sql)))));
    }

    [Test]
    public void SqlQuery_with_an_unknown_input_is_unknown_and_names_it()
    {
        var inputs = new[]
        {
            new SchemaInput(PortType.Table, true, SchemaResult.Of(Of(Customers()))),
            new SchemaInput(PortType.Table, true, SchemaResult.Fail("upstream is dynamic")),
            new SchemaInput(PortType.Table, false), new SchemaInput(PortType.Table, false),
        };
        var result = new SqlQueryNode().Infer("table", inputs, NodeTestHelpers.Params(("sql", "SELECT * FROM t1")), _catalog);
        Assert.That(result.Ok, Is.False);
        Assert.That(result.ToString(), Does.Contain("t2").And.Contain("upstream is dynamic"));
    }

    [Test]
    public void SqlQuery_with_no_t1_schema_is_unknown_and_names_t1()
    {
        var result = new SqlQueryNode().Infer("table", Wired(), NodeTestHelpers.Params(("sql", "SELECT 1")), _catalog);
        Assert.That(result.ToString(), Does.Contain("t1"));
    }

    [Test]
    public void SqlQuery_bad_sql_is_a_failed_result()
    {
        var inputs = Wired(Of(Customers()));
        Assert.That(new SqlQueryNode().Infer("table", inputs, NodeTestHelpers.Params(("sql", "SELECT Nope FROM t")), _catalog).Ok, Is.False);
        Assert.That(new SqlQueryNode().Infer("table", inputs, NodeTestHelpers.Params(("sql", "DELETE FROM t")), _catalog).ToString(),
            Does.Contain("sql.query"));
    }

    [TestCase("csv.read")]
    [TestCase("parquet.read")]
    [TestCase("json.read")]
    [TestCase("duck.read")]
    [TestCase("duck.table")]
    [TestCase("duck.query")]
    public void Readers_with_a_missing_file_an_empty_path_or_a_graph_input_give_unknown_with_a_message(string kind)
    {
        IFlowNode node = DuckDbNodes.All.Single(n => n.Spec.Kind == kind);
        foreach (var path in new[] { Path.Combine(_folder, "absent.csv"), "", "$source" })
        {
            var result = Infer(node, ("path", path), ("table", "items"), ("sql", "SELECT 1"));
            Assert.That(result.Ok, Is.False, path);
            Assert.That(result.ToString(), Does.Contain(kind), path);
        }
    }

    [Test]
    public void Every_inferred_output_in_the_pack_is_implemented()
    {
        foreach (var node in DuckDbNodes.All.Where(n => n.Spec.Schemas.OfType<InferredSchemaSpec>().Any()))
            Assert.That(node, Is.InstanceOf<ISchemaNode>(), node.Spec.Kind);
        Assert.That(DuckDbNodes.All.SelectMany(n => n.SchemaProblems()), Is.Empty);
    }

    [Test]
    public void CachedCatalog_reads_an_unchanged_file_once_and_an_edited_file_again()
    {
        var path = Path.Combine(Directory.CreateDirectory(Path.Combine(_folder, "cache")).FullName, "cached.csv");
        File.WriteAllText(path, "a,b\n1,x\n");
        var catalog = new CachedCatalog(new DuckDbCatalog(ConnectionRegistry.Of()));
        var node = new CsvReadNode();
        var ps = NodeTestHelpers.Params(("path", path));

        Assert.That(node.Infer("table", [], ps, catalog).Require().Columns, Has.Count.EqualTo(2));
        node.Infer("table", [], ps, catalog);
        Assert.That(catalog.Count, Is.EqualTo(1));

        File.WriteAllText(path, "a,b,c\n1,x,2.5\n");
        Assert.That(node.Infer("table", [], ps, catalog).Require().Columns, Has.Count.EqualTo(3));
        Assert.That(catalog.Count, Is.EqualTo(2));
    }

    [Test]
    public void CachedCatalog_keys_queries_by_text_and_input_schemas()
    {
        var catalog = new CachedCatalog(new DuckDbCatalog(ConnectionRegistry.Of()));
        var ints = new Schema([new Column("a", ColumnType.Integer)]);
        var texts = new Schema([new Column("a", ColumnType.Text)]);
        Assert.That(catalog.QuerySchema("SELECT * FROM t1", [ints]).Require().Columns[0].Type, Is.EqualTo(ColumnType.Integer));
        Assert.That(catalog.QuerySchema("SELECT * FROM t1", [texts]).Require().Columns[0].Type, Is.EqualTo(ColumnType.Text));
        catalog.QuerySchema("SELECT * FROM t1", [ints]);
        Assert.That(catalog.Count, Is.EqualTo(2));
    }
}
