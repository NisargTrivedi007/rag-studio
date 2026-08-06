namespace RagAndAI.Api.Services.FileParser;

public interface IFileParser
{
    Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default);
}
