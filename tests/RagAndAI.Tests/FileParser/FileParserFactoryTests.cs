using FluentAssertions;
using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class FileParserFactoryTests
{
    private readonly FileParserFactory _sut = new();

    [Theory]
    [InlineData(".txt",  typeof(TextParser))]
    [InlineData(".TXT",  typeof(TextParser))]
    [InlineData(".md",   typeof(TextParser))]
    [InlineData(".pdf",  typeof(PdfParser))]
    [InlineData(".docx", typeof(WordParser))]
    [InlineData(".xlsx", typeof(ExcelParser))]
    public void GetParser_ReturnsCorrectParserType(string extension, Type expectedType)
    {
        var parser = _sut.GetParser(extension);
        parser.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData(".pptx")]
    [InlineData(".zip")]
    [InlineData(".exe")]
    public void GetParser_ThrowsForUnsupportedExtension(string extension)
    {
        var act = () => _sut.GetParser(extension);
        act.Should().Throw<NotSupportedException>();
    }
}
