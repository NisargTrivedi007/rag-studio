namespace RagAndAI.Tests.Integration;

[Collection("Integration")]
public class HealthTests(ApiFixture fixture)
{
    [Fact]
    public async Task Get_Returns200_WithOkStatus()
    {
        var response = await fixture.Client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("ok", body, StringComparison.OrdinalIgnoreCase);
    }
}
