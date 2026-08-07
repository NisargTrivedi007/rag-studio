using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.FileParser;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Documents;

public class UploadEndpoint
{
    public static async Task<IResult> Handle(
        IFormFile file,
        FileParserFactory parserFactory,
        IRagService ragService,
        AppDbContext db,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return Results.BadRequest("File is empty");

        using var stream = file.OpenReadStream();
        var parser = parserFactory.GetParser(Path.GetExtension(file.FileName));
        var text = await parser.ExtractTextAsync(stream, file.FileName, ct);

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

        await ragService.IngestAsync(document.Id, text, ct);

        return Results.Ok(new DocumentUploadResponse(
            document.Id,
            document.Filename,
            document.FileType,
            document.UploadedAt));
    }
}

public record DocumentUploadResponse(
    Guid Id,
    string Filename,
    string FileType,
    DateTimeOffset UploadedAt);
