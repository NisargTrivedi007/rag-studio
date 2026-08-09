using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Sessions;

public class LibraryChatEndpoint
{
    /// <summary>
    /// Unlike session chat which scopes to uploaded files, this queries ALL library
    /// documents (SessionId == null) so users can chat across their entire knowledge base.
    /// </summary>
    public static async Task<IResult> Handle(
        Guid id,
        LibraryChatRequest request,
        IRagService ragService,
        AppDbContext db,
        CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
            return Results.NotFound();

        var libraryDocIds = await db.Documents
            .Where(d => d.SessionId == null)
            .Select(d => d.Id)
            .ToListAsync(ct);

        if (libraryDocIds.Count == 0)
            return Results.BadRequest("No documents in library. Upload files from the Library page.");

        var history = session.Messages
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        var result = await ragService.QueryAsync(request.Question, libraryDocIds, history, ct);

        db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = id,
            Role = "user",
            Content = request.Question,
            CreatedAt = DateTimeOffset.UtcNow
        });

        db.ChatMessages.Add(new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = id,
            Role = "assistant",
            Content = result.Answer,
            CreatedAt = DateTimeOffset.UtcNow
        });

        if (session.Title == null)
        {
            session.Title = request.Question.Length > 60
                ? request.Question[..60]
                : request.Question;
        }

        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new SessionChatResponse(result.Answer, result.Sources.ToList()));
    }
}

public record LibraryChatRequest(string Question);
