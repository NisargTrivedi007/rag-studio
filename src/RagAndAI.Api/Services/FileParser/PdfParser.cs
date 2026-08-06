using System.Text;
using UglyToad.PdfPig;

namespace RagAndAI.Api.Services.FileParser;

public class PdfParser : IFileParser
{
    public Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        var bytes = memoryStream.ToArray();

        using var document = PdfDocument.Open(bytes);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return Task.FromResult(sb.ToString().Trim());
    }
}
