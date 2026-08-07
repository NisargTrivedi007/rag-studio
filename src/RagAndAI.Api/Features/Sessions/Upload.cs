using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.FileParser;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Sessions;

public class UploadEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        IFormFile file,
        FileParserFactory parserFactory,
        IRagService ragService,
        AppDbContext db,
        CancellationToken ct)
    {
        // Verify session exists
        var session = await db.Sessions.FindAsync([id], cancellationToken: ct);
        if (session == null)
            return Results.NotFound();

        if (file.Length == 0)
            return Results.BadRequest("File is empty");

        // Parse file
        using var stream = file.OpenReadStream();
        var parser = parserFactory.GetParser(Path.GetExtension(file.FileName));
        var text = await parser.ExtractTextAsync(stream, file.FileName, ct);

        // Create document scoped to session
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Filename = file.FileName,
            FileType = Path.GetExtension(file.FileName).TrimStart('.'),
            UploadedAt = DateTimeOffset.UtcNow,
            SessionId = id
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        // Ingest text
        await ragService.IngestAsync(document.Id, text, ct);

        return Results.Ok(new { id = document.Id, filename = document.Filename, fileType = document.FileType });
    }
}
