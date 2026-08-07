using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace RagAndAI.Api.Services.FileParser;

public class WordParser : IFileParser
{
    public Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return Task.FromResult(string.Empty);

        var sb = new StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }

        return Task.FromResult(sb.ToString().Trim());
    }
}
