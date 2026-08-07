using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Sessions;

public class ChatEndpoint
{
    public static async Task<IResult> Handle(
        Guid id,
        SessionChatRequest request,
        IRagService ragService,
        AppDbContext db,
        CancellationToken ct)
    {
        // Load session
        var session = await db.Sessions
            .Include(s => s.Documents)
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
            return Results.NotFound();

        // Get session document IDs
        var sessionDocIds = session.Documents.Select(d => d.Id).ToList();

        // Add optional library document IDs
        var allDocIds = new List<Guid>(sessionDocIds);
        if (request.LibraryDocumentIds != null && request.LibraryDocumentIds.Length > 0)
        {
            allDocIds.AddRange(request.LibraryDocumentIds);
        }

        // Verify at least one document
        if (allDocIds.Count == 0)
            return Results.BadRequest("No documents found for query");

        // Get last 10 messages for history (last 5 turns)
        var history = session.Messages
            .OrderByDescending(m => m.CreatedAt)
            .Take(10)
            .OrderBy(m => m.CreatedAt)
            .ToList();

        // Query with history
        var result = await ragService.QueryAsync(request.Question, allDocIds, history, ct);

        // Save user message
        var userMsg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = id,
            Role = "user",
            Content = request.Question,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ChatMessages.Add(userMsg);

        // Save assistant message
        var assistantMsg = new ChatMessage
        {
            Id = Guid.NewGuid(),
            SessionId = id,
            Role = "assistant",
            Content = result.Answer,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.ChatMessages.Add(assistantMsg);

        // Set title on first user message (first 60 chars of question)
        if (session.Title == null)
        {
            session.Title = request.Question.Length > 60
                ? request.Question.Substring(0, 60)
                : request.Question;
        }

        // Update session timestamp
        session.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(ct);

        return Results.Ok(new SessionChatResponse(result.Answer, result.Sources.ToList()));
    }
}

public record SessionChatRequest(
    string Question,
    Guid[]? LibraryDocumentIds = null);

public record SessionChatResponse(
    string Answer,
    List<string> Sources);
