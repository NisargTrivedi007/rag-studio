using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace RagAndAI.Tests.Integration;

[Collection("Integration")]
public class SqlTests(ApiFixture fixture)
{
    [Fact]
    public async Task Schema_Returns200_WithContent()
    {
        var response = await fixture.Client.GetAsync("/sql/schema");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("schema").GetString()));
    }

    [Fact]
    public async Task Query_ReturnsResults_ForSimpleQuestion()
    {
        var response = await fixture.Client.PostAsJsonAsync("/sql/query", new
        {
            question = "How many customers are there?"
        });
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("sql").GetString()));
        Assert.True(json.GetProperty("results").GetArrayLength() > 0);
    }
}
