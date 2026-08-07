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
    AppDbContext db)
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
        var sql = response.Content?.Trim() ?? "";

        // Validate SQL
        if (!validator.Validate(sql, out var error))
            throw new InvalidOperationException($"SQL validation failed: {error}");

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

        return new NlToSqlResult(sql, results);
    }
}

public record NlToSqlResult(
    string Sql,
    List<Dictionary<string, object>> Results);
