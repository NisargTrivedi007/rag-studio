namespace RagAndAI.Api.Services.NlToSql;

public class SqlPromptBuilder
{
    public string Build(string question, string schema)
    {
        return $"""
            You are a PostgreSQL query expert. Generate a valid PostgreSQL SELECT query to answer the following question.

            Database Schema:
            {schema}

            Question: {question}

            Return ONLY the SQL SELECT statement, nothing else. Do not include markdown formatting or explanations.
            """;
    }
}
