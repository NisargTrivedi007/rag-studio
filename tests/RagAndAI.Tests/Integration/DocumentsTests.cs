using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RagAndAI.Tests.Integration;

[Collection("Integration")]
public class DocumentsTests(ApiFixture fixture)
{
    private static readonly string PdfPath = Path.Combine(
        AppContext.BaseDirectory, "Files", "Static_web_quote_redacted.pdf");

    private MultipartFormDataContent BuildPdfForm() =>
        new() { { new StreamContent(File.OpenRead(PdfPath)), "file", "test.pdf" } };

    private async Task<Guid> UploadAsync()
    {
        using var form = BuildPdfForm();
        var response = await fixture.Client.PostAsync("/documents/upload", form);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    private Task DeleteAsync(Guid id) =>
        fixture.Client.DeleteAsync($"/documents/{id}");

    [Fact]
    public async Task Upload_Returns200_WithDocumentId()
    {
        using var form = BuildPdfForm();
        var response = await fixture.Client.PostAsync("/documents/upload", form);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = json.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, id);

        await DeleteAsync(id);
    }

    [Fact]
    public async Task Upload_AppearsInList()
    {
        var id = await UploadAsync();

        var response = await fixture.Client.GetAsync("/documents/");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(list!, d => d.GetProperty("id").GetGuid() == id);

        await DeleteAsync(id);
    }

    [Fact]
    public async Task Delete_Returns204()
    {
        var id = await UploadAsync();

        var response = await fixture.Client.DeleteAsync($"/documents/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForUnknownId()
    {
        var response = await fixture.Client.DeleteAsync($"/documents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        using var form = new MultipartFormDataContent
        {
            { new StreamContent(Stream.Null), "file", "empty.pdf" }
        };
        var response = await fixture.Client.PostAsync("/documents/upload", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
