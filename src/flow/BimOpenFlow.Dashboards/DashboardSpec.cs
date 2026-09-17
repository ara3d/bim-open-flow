using System;
using System.Collections.Generic;
using System.Text.Json;

namespace BimOpenFlow.Dashboards;

public enum DashboardWidget
{
    Table,
    BarChart,
    LineChart,
}

/// <summary>
/// One dashboard widget: which recorded output table ("nodeId.port"), which
/// viz component, and its mount options as raw JSON (the viz option shapes:
/// Table {maxRows, sortable}; BarChart {width, height, categoryColumn,
/// valueColumn}; LineChart {width, height, xColumn, seriesColumns}).
/// </summary>
public sealed record DashboardItem(
    string OutputPort,
    DashboardWidget Widget,
    string? Title = null,
    string? OptionsJson = null)
{
    public string? OptionsJson { get; } = OptionsJson is null || IsValidJson(OptionsJson)
        ? OptionsJson
        : throw new ArgumentException($"OptionsJson for '{OutputPort}' is not valid JSON", nameof(OptionsJson));

    private static bool IsValidJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return doc.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public sealed record DashboardSpec(string Title, IReadOnlyList<DashboardItem> Items);
