using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RagAndAI.Tests.Integration;

[Collection("Integration")]
public class ChatTests(ApiFixture fixture)
{
    private static readonly string PdfPath = Path.Combine(
        AppContext.BaseDirectory, "Files", "Static_web_quote_redacted.pdf");

    private MultipartFormDataContent BuildPdfForm() =>
        new() { { new StreamContent(File.OpenRead(PdfPath)), "file", "test.pdf" } };

    private async Task<Guid> UploadLibraryDocAsync()
    {
        using var form = BuildPdfForm();
        var response = await fixture.Client.PostAsync("/documents/upload", form);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateSessionAsync()
    {
        var response = await fixture.Client.PostAsync("/sessions/", null);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task LibraryChat_ReturnsAnswer()
    {
        var docId = await UploadLibraryDocAsync();

        var response = await fixture.Client.PostAsJsonAsync("/chat/", new
        {
            question = "What is this document about?",
            documentIds = new[] { docId }
        });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("answer").GetString()));

        await fixture.Client.DeleteAsync($"/documents/{docId}");
    }

    [Fact]
    public async Task SessionChat_ReturnsAnswer()
    {
        var sessionId = await CreateSessionAsync();

        using var form = BuildPdfForm();
        var uploadResponse = await fixture.Client.PostAsync($"/sessions/{sessionId}/upload", form);
        uploadResponse.EnsureSuccessStatusCode();

        var chatResponse = await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/chat", new
        {
            question = "What is this document about?"
        });
        var json = await chatResponse.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, chatResponse.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("answer").GetString()));

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
    }

    [Fact]
    public async Task SessionChat_Returns404_ForUnknownSession()
    {
        var response = await fixture.Client.PostAsJsonAsync($"/sessions/{Guid.NewGuid()}/chat", new
        {
            question = "Hello?"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SessionChat_Returns400_WhenSessionHasNoDocuments()
    {
        var sessionId = await CreateSessionAsync();

        var response = await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/chat", new
        {
            question = "Hello?"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
    }

    [Fact]
    public async Task LibraryChatEndpoint_Returns404_ForUnknownSession()
    {
        var response = await fixture.Client.PostAsJsonAsync($"/sessions/{Guid.NewGuid()}/library-chat", new
        {
            question = "Hello?"
        });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LibraryChatEndpoint_SavesUserAndAssistantMessages()
    {
        var docId = await UploadLibraryDocAsync();
        var sessionId = await CreateSessionAsync();

        await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/library-chat", new
        {
            question = "What is this document about?"
        });

        var sessionResponse = await fixture.Client.GetAsync($"/sessions/{sessionId}");
        var json = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messages = json.GetProperty("messages");

        Assert.Equal(2, messages.GetArrayLength());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[1].GetProperty("role").GetString());

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
        await fixture.Client.DeleteAsync($"/documents/{docId}");
    }

    [Fact]
    public async Task LibraryChatEndpoint_SetsSessionTitle_OnFirstMessage()
    {
        var docId = await UploadLibraryDocAsync();
        var sessionId = await CreateSessionAsync();

        await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/library-chat", new
        {
            question = "What is this document about?"
        });

        var sessionResponse = await fixture.Client.GetAsync($"/sessions/{sessionId}");
        var json = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var title = json.GetProperty("title").GetString();

        Assert.False(string.IsNullOrWhiteSpace(title));

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
        await fixture.Client.DeleteAsync($"/documents/{docId}");
    }

    [Fact]
    public async Task LibraryChatEndpoint_TitleTruncatedAt60Chars()
    {
        var docId = await UploadLibraryDocAsync();
        var sessionId = await CreateSessionAsync();
        var longQuestion = new string('A', 80);

        await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/library-chat", new
        {
            question = longQuestion
        });

        var sessionResponse = await fixture.Client.GetAsync($"/sessions/{sessionId}");
        var json = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var title = json.GetProperty("title").GetString();

        Assert.NotNull(title);
        Assert.True(title.Length <= 60);

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
        await fixture.Client.DeleteAsync($"/documents/{docId}");
    }

    [Fact]
    public async Task LibraryChatEndpoint_MessagesReturnedInChronologicalOrder()
    {
        var docId = await UploadLibraryDocAsync();
        var sessionId = await CreateSessionAsync();

        await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/library-chat", new { question = "First question?" });
        await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/library-chat", new { question = "Second question?" });

        var sessionResponse = await fixture.Client.GetAsync($"/sessions/{sessionId}");
        var json = await sessionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var messages = json.GetProperty("messages");

        Assert.Equal(4, messages.GetArrayLength()); // 2 user + 2 assistant
        var timestamps = Enumerable.Range(0, 4)
            .Select(i => messages[i].GetProperty("createdAt").GetString())
            .ToList();
        Assert.Equal(timestamps.OrderBy(t => t).ToList(), timestamps);

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
        await fixture.Client.DeleteAsync($"/documents/{docId}");
    }

    [Fact]
    public async Task LibraryChatEndpoint_Returns400_WhenNoLibraryDocs()
    {
        // Ensure no library docs exist by using a fresh session against an empty library state.
        // This test is only reliable if the library is empty — it checks the guard, not RAG.
        var sessionId = await CreateSessionAsync();

        // Delete all library docs first so the guard fires
        var listResponse = await fixture.Client.GetAsync("/documents/");
        var docs = await listResponse.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        foreach (var doc in docs ?? [])
            await fixture.Client.DeleteAsync($"/documents/{doc.GetProperty("id").GetString()}");

        var response = await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/library-chat", new
        {
            question = "What is in the library?"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
    }

    [Fact]
    public async Task LibraryChatEndpoint_ReturnsAnswer()
    {
        var docId = await UploadLibraryDocAsync();
        var sessionId = await CreateSessionAsync();

        var response = await fixture.Client.PostAsJsonAsync($"/sessions/{sessionId}/library-chat", new
        {
            question = "What is this document about?"
        });
        var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("answer").GetString()));

        await fixture.Client.DeleteAsync($"/sessions/{sessionId}");
        await fixture.Client.DeleteAsync($"/documents/{docId}");
    }
}
