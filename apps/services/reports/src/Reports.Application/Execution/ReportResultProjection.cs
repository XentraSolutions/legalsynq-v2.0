using System.Text.Json;
using Reports.Contracts.Adapters;

namespace Reports.Application.Execution;

/// <summary>
/// Applies a tenant view's configured report columns to the normalized result
/// set. Keeping this before response/export construction guarantees every
/// output format receives the same visible fields in the same order.
/// </summary>
public static class ReportResultProjection
{
    public static (List<TabularColumn> Columns, List<Dictionary<string, object?>> Rows) Apply(
        IReadOnlyList<TabularColumn> columns,
        IReadOnlyList<Dictionary<string, object?>> rows,
        string? columnConfigJson)
    {
        if (!TryParse(columnConfigJson, out var configuredColumns))
            return (columns.ToList(), rows.ToList());

        var available = columns.ToDictionary(column => column.Key, StringComparer.OrdinalIgnoreCase);
        var selected = configuredColumns
            .Where(configured => configured.Visible)
            .Where(configured => available.ContainsKey(configured.Key))
            .OrderBy(configured => configured.Order)
            .ThenBy(configured => configured.Sequence)
            .Select(configured =>
            {
                var source = available[configured.Key];
                return new TabularColumn
                {
                    Key = source.Key,
                    Label = string.IsNullOrWhiteSpace(configured.Label) ? source.Label : configured.Label,
                    DataType = source.DataType,
                    Order = configured.Order,
                };
            })
            .ToList();

        var projectedRows = rows.Select(row =>
        {
            var projected = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in selected)
            {
                if (row.TryGetValue(column.Key, out var value))
                    projected[column.Key] = value;
            }

            return projected;
        }).ToList();

        return (selected, projectedRows);
    }

    private static bool TryParse(string? columnConfigJson, out List<ConfiguredColumn> columns)
    {
        columns = [];
        if (string.IsNullOrWhiteSpace(columnConfigJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(columnConfigJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sequence = 0;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var key = ReadString(item, "name") ?? ReadString(item, "key");
                if (string.IsNullOrWhiteSpace(key) || !seen.Add(key.Trim()))
                    continue;

                columns.Add(new ConfiguredColumn(
                    key.Trim(),
                    ReadString(item, "label"),
                    ReadBoolean(item, "visible") ?? true,
                    ReadInt32(item, "order") ?? sequence,
                    sequence));
                sequence++;
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? ReadBoolean(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) &&
        (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            ? value.GetBoolean()
            : null;

    private static int? ReadInt32(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private sealed record ConfiguredColumn(string Key, string? Label, bool Visible, int Order, int Sequence);
}
