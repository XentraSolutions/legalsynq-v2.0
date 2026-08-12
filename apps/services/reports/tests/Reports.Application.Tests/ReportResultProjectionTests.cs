using Reports.Application.Execution;
using Reports.Contracts.Adapters;
using Reports.Contracts.Export;
using Reports.Infrastructure.Exporters;
using System.Text;

namespace Reports.Application.Tests;

public sealed class ReportResultProjectionTests
{
    [Fact]
    public void Apply_uses_visible_known_columns_in_configured_order_and_labels()
    {
        var columns = new List<TabularColumn>
        {
            new() { Key = "caseNumber", Label = "Case number", DataType = "string", Order = 0 },
            new() { Key = "clientName", Label = "Client", DataType = "string", Order = 1 },
            new() { Key = "balance", Label = "Balance", DataType = "decimal", Order = 2 },
        };
        var rows = new List<Dictionary<string, object?>>
        {
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["caseNumber"] = "CASE-1",
                ["clientName"] = "Ada Client",
                ["balance"] = 120m,
            },
        };

        var (projectedColumns, projectedRows) = ReportResultProjection.Apply(columns, rows, """
            [
              { "name": "balance", "label": "Outstanding", "order": 1, "visible": true },
              { "name": "caseNumber", "label": "Case", "order": 0, "visible": true },
              { "name": "caseNumber", "label": "Duplicate", "order": 2, "visible": true },
              { "name": "clientName", "visible": false },
              { "name": "missing", "visible": true }
            ]
            """);

        Assert.Equal(["caseNumber", "balance"], projectedColumns.Select(column => column.Key));
        Assert.Equal(["Case", "Outstanding"], projectedColumns.Select(column => column.Label));
        Assert.Equal(["caseNumber", "balance"], projectedRows.Single().Keys);
    }

    [Fact]
    public async Task Apply_produces_csv_with_only_selected_columns_in_configured_order()
    {
        var columns = new List<TabularColumn>
        {
            new() { Key = "caseNumber", Label = "Case number", Order = 0 },
            new() { Key = "clientName", Label = "Client", Order = 1 },
            new() { Key = "balance", Label = "Balance", Order = 2 },
        };
        var rows = new List<Dictionary<string, object?>>
        {
            new() { ["caseNumber"] = "CASE-1", ["clientName"] = "Hidden client", ["balance"] = 120m },
        };

        var (projectedColumns, projectedRows) = ReportResultProjection.Apply(columns, rows, """
            [
              { "name": "balance", "label": "Outstanding", "order": 1, "visible": true },
              { "name": "caseNumber", "label": "Case", "order": 0, "visible": true },
              { "name": "clientName", "visible": false }
            ]
            """);

        var export = await new CsvReportExporter().ExportAsync(new TabularResultSet
        {
            Columns = projectedColumns,
            Rows = projectedRows,
        }, new ExportContext { TemplateCode = "cases" });

        var csv = Encoding.UTF8.GetString(export.FileContent).TrimStart('\uFEFF').Replace("\r\n", "\n").TrimEnd();
        Assert.Equal("Case,Outstanding\nCASE-1,120", csv);
    }

    [Fact]
    public void Apply_keeps_legacy_result_when_config_is_missing_or_malformed()
    {
        var columns = new List<TabularColumn> { new() { Key = "caseNumber", Label = "Case", Order = 0 } };
        var rows = new List<Dictionary<string, object?>> { new() { ["caseNumber"] = "CASE-1" } };

        var missing = ReportResultProjection.Apply(columns, rows, null);
        var malformed = ReportResultProjection.Apply(columns, rows, "not-json");

        Assert.Single(missing.Columns);
        Assert.Single(malformed.Columns);
        Assert.Equal("CASE-1", missing.Rows.Single()["caseNumber"]);
        Assert.Equal("CASE-1", malformed.Rows.Single()["caseNumber"]);
    }
}
