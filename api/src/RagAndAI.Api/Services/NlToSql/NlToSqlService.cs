using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;
using Npgsql;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Services.NlToSql;

public class NlToSqlService(
    SchemaInspector schemaInspector,
    SqlPromptBuilder promptBuilder,
    SqlValidator validator,
    IChatCompletionService chatService,
    AppDbContext db,
    ILogger<NlToSqlService> logger)
{
    public async Task<NlToSqlResult> ExecuteAsync(string question, CancellationToken ct = default)
    {
        // Get schema
        var schema = await schemaInspector.GetSchemaAsync(ct);

        // Build prompt
        var prompt = promptBuilder.Build(question, schema);

        // Call LLM
        var history = new ChatHistory();
        history.AddUserMessage(prompt);
        var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
        var sql = StripMarkdownFences(response.Content?.Trim() ?? "");

        // Validate SQL
        logger.LogDebug("LLM generated SQL: {Sql}", sql);
        if (!validator.Validate(sql, out var error))
        {
            logger.LogWarning("SQL validation failed. Generated SQL: {Sql} | Error: {Error}", sql, error);
            throw new InvalidOperationException($"SQL validation failed: {error}");
        }

        // Execute via raw connection
        var results = new List<Dictionary<string, object>>();
        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);
        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync(ct);

            var fieldNames = Enumerable.Range(0, reader.FieldCount).Select(i => reader.GetName(i)).ToList();
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object>();
                for (int i = 0; i < reader.FieldCount; i++)
                    row[fieldNames[i]] = reader.GetValue(i);
                results.Add(row);
            }
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }

        var summary = await GenerateSummaryAsync(question, sql, results, ct);
        return new NlToSqlResult(sql, results, summary);
    }

    private async Task<string> GenerateSummaryAsync(
        string question,
        string sql,
        List<Dictionary<string, object>> results,
        CancellationToken ct)
    {
        try
        {
            var resultPreview = results.Count == 0
                ? "No rows returned."
                : System.Text.Json.JsonSerializer.Serialize(results.Take(20));

            var summaryPrompt = $"""
                A user asked: "{question}"
                The database returned {results.Count} row(s).
                Data: {resultPreview}

                Write a clear, friendly 1-3 sentence answer in plain English.
                Do not mention SQL, tables, or column names.
                Be specific about the numbers or values in the data.
                """;

            var history = new ChatHistory();
            history.AddUserMessage(summaryPrompt);
            var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
            return response.Content?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Summary generation failed; returning empty summary");
            return "";
        }
    }

    private static string StripMarkdownFences(string sql)
    {
        // LLMs sometimes wrap output in ```sql ... ``` despite being told not to
        var lines = sql.Split('\n');
        if (lines.Length >= 2 && lines[0].TrimStart().StartsWith("```"))
        {
            var inner = lines[^1].Trim() == "```"
                ? lines[1..^1]
                : lines[1..];
            return string.Join('\n', inner).Trim();
        }
        return sql;
    }
}

public record NlToSqlResult(
    string Sql,
    List<Dictionary<string, object>> Results,
    string Summary);
