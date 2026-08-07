using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RagAndAI.Tests.Integration;

/// <summary>
/// End-to-end user flow scenarios hitting the full stack.
/// </summary>
[Collection("Integration")]
public class ScenarioTests(ApiFixture fixture)
{
    private static readonly string Pdf1 = Path.Combine(AppContext.BaseDirectory, "Files", "Static_web_quote_redacted.pdf");
    private static readonly string Pdf2 = Path.Combine(AppContext.BaseDirectory, "Files", "project_full_feature_quote_redacted.pdf");

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<Guid> UploadLibraryDocAsync(string path)
    {
        using var form = new MultipartFormDataContent
        {
            { new StreamContent(File.OpenRead(path)), "file", Path.GetFileName(path) }
        };
        var r = await fixture.Client.PostAsync("/documents/upload", form);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateSessionAsync()
    {
        var r = await fixture.Client.PostAsync("/sessions/", null);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<Guid> UploadSessionDocAsync(Guid sessionId, string path)
    {
        using var form = new MultipartFormDataContent
        {
            { new StreamContent(File.OpenRead(path)), "file", Path.GetFileName(path) }
        };
        var r = await fixture.Client.PostAsync($"/sessions/{sessionId}/upload", form);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> SessionChatAsync(Guid sessionId, string question, Guid[]? libraryDocIds = null)
    {
        var r = await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/chat", new
        {
            question,
            libraryDocumentIds = libraryDocIds ?? []
        });
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ── scenarios ─────────────────────────────────────────────────────────────

    /// Upload a document, ask a question, verify answer and sources both come back.
    [Fact]
    public async Task UploadAndChat_ReturnsAnswerWithSources()
    {
        var docId = await UploadLibraryDocAsync(Pdf1);

        var r = await fixture.Client.PostAsJsonAsync("/chat/", new
        {
            question = "What is this document about?",
            documentIds = new[] { docId }
        });
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("answer").GetString()));
        Assert.True(json.GetProperty("sources").GetArrayLength() > 0);

        await fixture.Client.DeleteAsync($"/documents/{docId}");
    }

    /// Create session, upload doc, ask two follow-up questions.
    /// Verify the session stores all 4 messages and auto-titles from the first question.
    [Fact]
    public async Task SessionConversation_SavesHistoryAndAutoTitle()
    {
        var sessionId = await CreateSessionAsync();
        await UploadSessionDocAsync(sessionId, Pdf1);

        const string firstQuestion = "What is this document about?";
        await SessionChatAsync(sessionId, firstQuestion);
        await SessionChatAsync(sessionId, "Can you summarize the key points?");

        var detail = await fixture.Client.GetAsync($"/sessions/{sessionId}");
        var json = await detail.Content.ReadFromJsonAsync<JsonElement>();

        var messages = json.GetProperty("messages");
        Assert.Equal(4, messages.GetArrayLength()); // 2 user + 2 assistant

        var title = json.GetProperty("title").GetString();
        Assert.False(string.IsNullOrWhiteSpace(title));
        Assert.StartsWith(firstQuestion[..Math.Min(10, firstQuestion.Length)], title, StringComparison.OrdinalIgnoreCase);

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
    }

    /// Session chat using a library document via libraryDocumentIds (no session-scoped doc).
    [Fact]
    public async Task SessionChat_WithLibraryDoc_ReturnsAnswer()
    {
        var docId = await UploadLibraryDocAsync(Pdf1);
        var sessionId = await CreateSessionAsync();

        var json = await SessionChatAsync(sessionId, "What is this document about?", [docId]);

        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("answer").GetString()));

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
        await fixture.Client.DeleteAsync($"/documents/{docId}");
    }

    /// Upload two different documents, query across both in a single chat request.
    [Fact]
    public async Task Chat_AcrossMultipleDocs_ReturnsAnswer()
    {
        var docId1 = await UploadLibraryDocAsync(Pdf1);
        var docId2 = await UploadLibraryDocAsync(Pdf2);

        var r = await fixture.Client.PostAsJsonAsync("/chat/", new
        {
            question = "What topics are covered across these documents?",
            documentIds = new[] { docId1, docId2 }
        });
        var json = await r.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("answer").GetString()));

        await fixture.Client.DeleteAsync($"/documents/{docId1}");
        await fixture.Client.DeleteAsync($"/documents/{docId2}");
    }

    /// Chat with no document IDs must return 400.
    [Fact]
    public async Task Chat_WithNoDocumentIds_Returns400()
    {
        var r = await fixture.Client.PostAsJsonAsync("/chat/", new
        {
            question = "What is this about?",
            documentIds = Array.Empty<Guid>()
        });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    /// Session chat with no docs (session empty, no libraryDocumentIds) must return 400.
    [Fact]
    public async Task SessionChat_WithNoDocuments_Returns400()
    {
        var sessionId = await CreateSessionAsync();

        var r = await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/chat", new
        {
            question = "What is this about?"
        });

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
    }
}
