using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Services.NlToSql;

public class SchemaInspector(AppDbContext db)
{
    public async Task<string> GetSchemaAsync(CancellationToken ct = default)
    {
        var schema = new StringBuilder();
        schema.AppendLine("# Database Schema");
        schema.AppendLine();

        var conn = (NpgsqlConnection)db.Database.GetDbConnection();
        var wasOpen = conn.State == ConnectionState.Open;
        if (!wasOpen) await conn.OpenAsync(ct);

        try
        {
            await using var cmd = new NpgsqlCommand("""
                SELECT table_name::text, column_name::text, data_type::text, (is_nullable = 'YES') AS is_nullable
                FROM information_schema.columns
                WHERE table_schema = 'public'
                ORDER BY table_name, ordinal_position
                """, conn);

            await using var reader = await cmd.ExecuteReaderAsync(ct);

            string? currentTable = null;
            while (await reader.ReadAsync(ct))
            {
                var tableName = reader.GetString(0);
                var columnName = reader.GetString(1);
                var dataType = reader.GetString(2);
                var isNullable = reader.GetBoolean(3);

                if (tableName != currentTable)
                {
                    if (currentTable is not null) schema.AppendLine();
                    schema.AppendLine($"## {tableName}");
                    currentTable = tableName;
                }
                schema.AppendLine($"- {columnName}: {dataType} ({(isNullable ? "nullable" : "NOT NULL")})");
            }
        }
        finally
        {
            if (!wasOpen) await conn.CloseAsync();
        }

        return schema.ToString();
    }
}
