using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class ExcelParserTests
{
    private readonly ExcelParser _sut = new();

    private static MemoryStream BuildXlsx(Action<WorkbookPart> configure)
    {
        var ms = new MemoryStream();
        using var doc = SpreadsheetDocument.Create(ms, SpreadsheetDocumentType.Workbook, true);
        var workbookPart = doc.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        configure(workbookPart);
        workbookPart.Workbook.Save();
        ms.Position = 0;
        return ms;
    }

    private static WorksheetPart AddSheet(WorkbookPart workbookPart, SheetData sheetData)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        worksheetPart.Worksheet = new Worksheet(sheetData);
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        sheets.AppendChild(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "Sheet1"
        });
        return worksheetPart;
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsInlineValues()
    {
        using var ms = BuildXlsx(wb =>
        {
            var sheetData = new SheetData(new Row(
                new Cell { CellValue = new CellValue("42"), DataType = CellValues.Number },
                new Cell { CellValue = new CellValue("99"), DataType = CellValues.Number }
            ));
            AddSheet(wb, sheetData);
        });

        var result = await _sut.ExtractTextAsync(ms, "test.xlsx");

        Assert.Contains("42", result);
        Assert.Contains("99", result);
    }

    [Fact]
    public async Task ExtractTextAsync_ResolvesSharedStrings()
    {
        using var ms = BuildXlsx(wb =>
        {
            var sharedPart = wb.AddNewPart<SharedStringTablePart>();
            sharedPart.SharedStringTable = new SharedStringTable(
                new SharedStringItem(new Text("Hello")),
                new SharedStringItem(new Text("World"))
            );
            sharedPart.SharedStringTable.Save();

            var sheetData = new SheetData(new Row(
                new Cell { CellValue = new CellValue("0"), DataType = CellValues.SharedString },
                new Cell { CellValue = new CellValue("1"), DataType = CellValues.SharedString }
            ));
            AddSheet(wb, sheetData);
        });

        var result = await _sut.ExtractTextAsync(ms, "test.xlsx");

        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
    }
}
