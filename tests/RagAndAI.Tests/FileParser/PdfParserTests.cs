using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class PdfParserTests
{
    private readonly PdfParser _sut = new();

    [Fact(Skip = "Place a sample.pdf in tests/Fixtures/ to run this test")]
    public async Task ExtractTextAsync_ReturnsText_FromValidPdf()
    {
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "sample.pdf");

        var pdfBytes = File.ReadAllBytes(fixturePath);
        using var stream = new MemoryStream(pdfBytes);

        var result = await _sut.ExtractTextAsync(stream, "test.pdf");
    }
}
