using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace RagAndAI.Api.Services.FileParser;

public class ExcelParser : IFileParser
{
    public Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var doc = SpreadsheetDocument.Open(stream, false);
        var workbook = doc.WorkbookPart;
        if (workbook is null) return Task.FromResult(string.Empty);

        var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable;
        var sb = new StringBuilder();

        foreach (var sheet in workbook.WorksheetParts)
        {
            var rows = sheet.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>() ?? [];
            foreach (var row in rows)
            {
                var cells = row.Elements<Cell>()
                    .Select(c => GetCellValue(c, sharedStrings));
                sb.AppendLine(string.Join("\t", cells));
            }
        }

        return Task.FromResult(sb.ToString().Trim());
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        var value = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString
            && sharedStrings is not null
            && int.TryParse(value, out var index))
        {
            return sharedStrings.ElementAt(index).InnerText;
        }
        return value;
    }
}
