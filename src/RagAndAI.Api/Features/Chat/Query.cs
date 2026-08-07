using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Chat;

public class QueryEndpoint
{
    public static async Task<ChatQueryResponse> Handle(
        ChatQueryRequest request,
        IRagService ragService,
        CancellationToken ct)
    {
        if (request.DocumentIds == null || request.DocumentIds.Length == 0)
            throw new ArgumentException("At least one document ID required");

        var result = await ragService.QueryAsync(request.Question, request.DocumentIds, ct);

        return new ChatQueryResponse(result.Answer, result.Sources.ToList());
    }
}

public record ChatQueryRequest(
    string Question,
    Guid[] DocumentIds);

public record ChatQueryResponse(
    string Answer,
    List<string> Sources);
