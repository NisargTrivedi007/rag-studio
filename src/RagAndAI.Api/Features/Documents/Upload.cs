using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.FileParser;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Documents;

public class UploadEndpoint
{
    public static async Task<DocumentUploadResponse> Handle(
        IFormFile file,
        FileParserFactory parserFactory,
        IRagService ragService,
        AppDbContext db,
        CancellationToken ct)
    {
        if (file.Length == 0)
            throw new ArgumentException("File is empty");

        // Parse file
        using var stream = file.OpenReadStream();
        var parser = parserFactory.GetParser(Path.GetExtension(file.FileName));
        var text = await parser.ExtractTextAsync(stream, file.FileName, ct);

        // Create document record
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Filename = file.FileName,
            FileType = Path.GetExtension(file.FileName).TrimStart('.'),
            UploadedAt = DateTimeOffset.UtcNow,
            SessionId = null
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        // Ingest text
        await ragService.IngestAsync(document.Id, text, ct);

        return new DocumentUploadResponse(
            document.Id,
            document.Filename,
            document.FileType,
            document.UploadedAt);
    }
}

public record DocumentUploadResponse(
    Guid Id,
    string Filename,
    string FileType,
    DateTimeOffset UploadedAt);
