using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RagAndAI.Tests.Integration;

[Collection("Integration")]
public class SessionsTests(ApiFixture fixture)
{
    private async Task<Guid> CreateSessionAsync()
    {
        var response = await fixture.Client.PostAsync("/sessions/", null);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    private Task DeleteSessionAsync(Guid id) =>
        fixture.Client.DeleteAsync($"/sessions/{id}");

    [Fact]
    public async Task Create_Returns200_WithId()
    {
        var response = await fixture.Client.PostAsync("/sessions/", null);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = json.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEqual(Guid.Empty, id);

        await DeleteSessionAsync(id);
    }

    [Fact]
    public async Task List_IncludesCreatedSession()
    {
        var id = await CreateSessionAsync();

        var response = await fixture.Client.GetAsync("/sessions/");
        var list = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(list!, s => s.GetProperty("id").GetGuid() == id);

        await DeleteSessionAsync(id);
    }

    [Fact]
    public async Task Get_Returns200_WithDetail()
    {
        var id = await CreateSessionAsync();

        var response = await fixture.Client.GetAsync($"/sessions/{id}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(id, json.GetProperty("id").GetGuid());

        await DeleteSessionAsync(id);
    }

    [Fact]
    public async Task Get_Returns404_ForUnknownId()
    {
        var response = await fixture.Client.GetAsync($"/sessions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns204()
    {
        var id = await CreateSessionAsync();

        var response = await fixture.Client.DeleteAsync($"/sessions/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Delete_Returns404_ForUnknownId()
    {
        var response = await fixture.Client.DeleteAsync($"/sessions/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
