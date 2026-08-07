namespace RagAndAI.Api.Features.Health;

public class CheckEndpoint
{
    public static IResult Handle()
    {
        return Results.Ok(new { status = "ok" });
    }
}
