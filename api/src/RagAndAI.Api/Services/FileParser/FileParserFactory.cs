namespace RagAndAI.Api.Services.FileParser;

public class FileParserFactory
{
    private static readonly Dictionary<string, IFileParser> Parsers = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".txt",  new TextParser() },
        { ".md",   new TextParser() },
        { ".csv",  new TextParser() },
        { ".pdf",  new PdfParser() },
        { ".docx", new WordParser() },
        { ".xlsx", new ExcelParser() },
    };

    public IFileParser GetParser(string fileExtension)
    {
        if (Parsers.TryGetValue(fileExtension, out var parser))
            return parser;

        throw new NotSupportedException(
            $"File type '{fileExtension}' is not supported. Supported: {string.Join(", ", Parsers.Keys)}");
    }
}
