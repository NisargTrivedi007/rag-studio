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
}
