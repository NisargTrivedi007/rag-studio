using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class WordParserTests
{
    private readonly WordParser _sut = new();

    private static MemoryStream BuildDocx(params string[] paragraphs)
    {
        var ms = new MemoryStream();
        using var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true);
        var main = doc.AddMainDocumentPart();
        main.Document = new Document(new Body(
            paragraphs.Select(t => new Paragraph(new Run(new Text(t)))).ToArray<OpenXmlElement>()
        ));
        main.Document.Save();
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsParagraphText()
    {
        using var ms = BuildDocx("Hello", "World");

        var result = await _sut.ExtractTextAsync(ms, "test.docx");

        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsEmpty_WhenNoParagraphs()
    {
        using var ms = BuildDocx();

        var result = await _sut.ExtractTextAsync(ms, "test.docx");

        Assert.Equal(string.Empty, result);
    }
}
