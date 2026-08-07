using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class PdfParserTests
{
    private readonly PdfParser _sut = new();

    [Theory]
    [InlineData("Static_web_quote_redacted.pdf")]
    [InlineData("project_full_feature_quote_redacted.pdf")]
    public async Task ExtractTextAsync_ReturnsNonEmptyText_FromPdf(string fileName)
    {
        var filePath = Path.Combine(AppContext.BaseDirectory, "Files", fileName);
        var pdfBytes = File.ReadAllBytes(filePath);
        using var stream = new MemoryStream(pdfBytes);

        var result = await _sut.ExtractTextAsync(stream, fileName);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }
}
