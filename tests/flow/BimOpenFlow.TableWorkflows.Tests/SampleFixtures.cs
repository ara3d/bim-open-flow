using ClosedXML.Excel;
using DuckDB.NET.Data;
using Microsoft.Data.Sqlite;

namespace BimOpenFlow.TableWorkflows.Tests;

/// <summary>One parsed sample CSV: the table name, header, and raw string cells.</summary>
public sealed record CsvTable(string Name, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// Generates the binary sample fixtures (sample.xlsx / sample.sqlite / sample.duckdb)
/// from the three committed CSVs. The [Explicit] seeder writes them next to the CSVs;
/// workflow tests write them into a temp dir through the same code path.
/// </summary>
public static class SampleFixtures
{
    public const string XlsxName = "sample.xlsx";
    public const string SqliteName = "sample.sqlite";
    public const string DuckDbName = "sample.duckdb";

    private static readonly IReadOnlyList<(string File, string Table)> Sources =
        [("customers", "Customers"), ("orders", "Orders"), ("products", "Products")];

    /// <summary>Writes all three binary fixtures from csvDir into outDir; returns the written paths.</summary>
    public static IReadOnlyList<string> SeedAll(string csvDir, string outDir)
    {
        var tables = ReadAll(csvDir);
        var xlsx = Path.Combine(outDir, XlsxName);
        var sqlite = Path.Combine(outDir, SqliteName);
        var duckDb = Path.Combine(outDir, DuckDbName);
        WriteXlsx(tables, xlsx);
        WriteSqlite(tables, sqlite);
        WriteDuckDb(tables, duckDb);
        return [xlsx, sqlite, duckDb];
    }

    public static IReadOnlyList<CsvTable> ReadAll(string csvDir)
        => Sources.Select(s => ReadCsv(Path.Combine(csvDir, s.File + ".csv"), s.Table)).ToList();

    /// <summary>Minimal CSV reader for the hand-authored samples (no quoting or escapes).</summary>
    public static CsvTable ReadCsv(string path, string name)
    {
        var lines = File.ReadAllLines(path).Where(l => l.Length > 0).ToList();
        return new(name,
            lines[0].Split(','),
            lines.Skip(1).Select(IReadOnlyList<string> (l) => l.Split(',')).ToList());
    }

    public static void WriteXlsx(IReadOnlyList<CsvTable> tables, string path)
    {
        File.Delete(path);
        using var workbook = new XLWorkbook();
        workbook.Properties.Author = "SampleDataSeeder";
        workbook.Properties.Created = new DateTime(2026, 1, 1);
        workbook.Properties.Modified = new DateTime(2026, 1, 1);
        foreach (var table in tables)
        {
            var sheet = workbook.AddWorksheet(table.Name);
            for (var c = 0; c < table.Columns.Count; c++)
                sheet.Cell(1, c + 1).Value = table.Columns[c];
            for (var r = 0; r < table.Rows.Count; r++)
                for (var c = 0; c < table.Columns.Count; c++)
                    sheet.Cell(r + 2, c + 1).Value = ToCellValue(table.Rows[r][c]);
        }
        workbook.SaveAs(path);
    }

    public static void WriteSqlite(IReadOnlyList<CsvTable> tables, string path)
    {
        File.Delete(path);
        using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        foreach (var table in tables)
        {
            var columns = string.Join(", ", table.Columns.Select((name, i) =>
                $"[{name}] {SqliteType(table, i)}"));
            Execute(connection, $"CREATE TABLE [{table.Name}] ({columns})");
            var slots = string.Join(", ", table.Columns.Select((_, i) => $"@p{i}"));
            foreach (var row in table.Rows)
            {
                using var insert = connection.CreateCommand();
                insert.CommandText = $"INSERT INTO [{table.Name}] VALUES ({slots})";
                for (var i = 0; i < row.Count; i++)
                    insert.Parameters.AddWithValue($"@p{i}", Parse(row[i]));
                insert.ExecuteNonQuery();
            }
        }
    }

    public static void WriteDuckDb(IReadOnlyList<CsvTable> tables, string path)
    {
        File.Delete(path);
        File.Delete(path + ".wal");
        using var connection = new DuckDBConnection($"Data Source={path}");
        connection.Open();
        foreach (var table in tables)
        {
            var columns = string.Join(", ", table.Columns.Select((name, i) =>
                $"\"{name}\" {DuckDbType(table, i)}"));
            Execute(connection, $"CREATE TABLE \"{table.Name}\" ({columns})");
            foreach (var row in table.Rows)
                Execute(connection,
                    $"INSERT INTO \"{table.Name}\" VALUES ({string.Join(", ", row.Select(Literal))})");
        }
    }

    private static void Execute(System.Data.Common.DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>Integer if every cell parses as long, Real if as double, else Text.</summary>
    private static Type ColumnType(CsvTable table, int column)
    {
        var cells = table.Rows.Select(r => r[column]).ToList();
        if (cells.All(c => long.TryParse(c, out _)))
            return typeof(long);
        if (cells.All(c => double.TryParse(c, System.Globalization.CultureInfo.InvariantCulture, out _)))
            return typeof(double);
        return typeof(string);
    }

    private static string SqliteType(CsvTable table, int column)
        => ColumnType(table, column) switch
        {
            var t when t == typeof(long) => "INTEGER",
            var t when t == typeof(double) => "REAL",
            _ => "TEXT",
        };

    private static string DuckDbType(CsvTable table, int column)
        => ColumnType(table, column) switch
        {
            var t when t == typeof(long) => "BIGINT",
            var t when t == typeof(double) => "DOUBLE",
            _ => "VARCHAR",
        };

    private static object Parse(string cell)
        => long.TryParse(cell, out var i) ? i
            : double.TryParse(cell, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d
            : cell;

    private static XLCellValue ToCellValue(string cell)
        => long.TryParse(cell, out var i) ? i
            : double.TryParse(cell, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d
            : cell;

    private static string Literal(string cell)
        => long.TryParse(cell, out _) || double.TryParse(cell, System.Globalization.CultureInfo.InvariantCulture, out _)
            ? cell
            : $"'{cell.Replace("'", "''")}'";
}
