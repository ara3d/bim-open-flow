using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Ara3D.DataTable;
using BimOpenFlow.Contracts;

namespace BimOpenFlow.Publishing;

/// <summary>
/// IDataTable to contracts TableData and its JSON form
/// ({columns: [{name, type}], rows: [[...]]}, camelCase keys, PascalCase
/// ColumnType values, invariant) — the shape the viz components consume.
/// Non-finite Numbers serialize as the strings "NaN"/"Infinity"/"-Infinity",
/// null cells as JSON null. Deterministic: same table, same bytes.
/// </summary>
public static class TableJson
{
    public static TableData ToTableData(this IDataTable table)
    {
        var columns = new List<ColumnSchema>(table.Columns.Count);
        foreach (var c in table.Columns)
            columns.Add(new(c.Descriptor.Name, ToColumnType(c.Descriptor.Type)));

        var rowCount = table.Columns.Count == 0 ? 0 : table.Columns[0].Count;
        var rows = new List<IReadOnlyList<object>>(rowCount);
        for (var r = 0; r < rowCount; r++)
        {
            var row = new object[table.Columns.Count];
            for (var c = 0; c < row.Length; c++)
                row[c] = table.Columns[c][r];
            rows.Add(row);
        }
        return new(columns, rows);
    }

    public static string ToJson(this IDataTable table)
        => table.ToTableData().ToJson();

    public static string ToJson(this TableData data)
    {
        using var stream = new MemoryStream();
        using (var w = new Utf8JsonWriter(stream))
        {
            w.WriteStartObject();
            w.WritePropertyName("columns");
            w.WriteStartArray();
            foreach (var c in data.Columns)
            {
                w.WriteStartObject();
                w.WriteString("name", c.Name);
                w.WriteString("type", c.Type.ToString());
                w.WriteEndObject();
            }
            w.WriteEndArray();
            w.WritePropertyName("rows");
            w.WriteStartArray();
            for (var r = 0; r < data.Rows.Count; r++)
            {
                var row = data.Rows[r];
                w.WriteStartArray();
                for (var c = 0; c < row.Count; c++)
                    WriteCell(w, data.Columns[c].Type, row[c]);
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>Maps a CLR column type to the contracts ColumnType (Nullable unwrapped).</summary>
    public static ColumnType ToColumnType(Type type)
        => TryToColumnType(type, out var result)
            ? result
            : throw new ArgumentException($"No ColumnType mapping for CLR type {type.Name}", nameof(type));

    public static bool TryToColumnType(Type type, out ColumnType result)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        result = ColumnType.Text;
        if (t == typeof(bool))
            result = ColumnType.Boolean;
        else if (t == typeof(sbyte) || t == typeof(byte) || t == typeof(short) || t == typeof(ushort)
            || t == typeof(int) || t == typeof(uint) || t == typeof(long))
            result = ColumnType.Integer;
        else if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
            result = ColumnType.Number;
        else if (t != typeof(string) && t != typeof(char))
            return false;
        return true;
    }

    private static void WriteCell(Utf8JsonWriter w, ColumnType type, object? cell)
    {
        if (cell is null or DBNull)
        {
            w.WriteNullValue();
            return;
        }
        switch (type)
        {
            case ColumnType.Boolean: w.WriteBooleanValue((bool)cell); break;
            case ColumnType.Integer: w.WriteNumberValue(Convert.ToInt64(cell)); break;
            case ColumnType.Number: WriteNumber(w, Convert.ToDouble(cell)); break;
            case ColumnType.Text: w.WriteStringValue(cell as string ?? ((char)cell).ToString()); break;
            default: throw new ArgumentOutOfRangeException(nameof(type));
        }
    }

    private static void WriteNumber(Utf8JsonWriter w, double value)
    {
        if (double.IsNaN(value))
            w.WriteStringValue("NaN");
        else if (double.IsPositiveInfinity(value))
            w.WriteStringValue("Infinity");
        else if (double.IsNegativeInfinity(value))
            w.WriteStringValue("-Infinity");
        else
            w.WriteNumberValue(value);
    }
}
