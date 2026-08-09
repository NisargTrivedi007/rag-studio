using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data;
using RagAndAI.Api.Features.Chat;
using RagAndAI.Api.Features.Documents;
using RagAndAI.Api.Features.Health;
using RagAndAI.Api.Features.Sessions;
using RagAndAI.Api.Features.SqlQuery;
using RagAndAI.Api.Services.FileParser;
using RagAndAI.Api.Services.NlToSql;
using RagAndAI.Api.Services.Rag;

var builder = WebApplication.CreateBuilder(args);

// CORS service
builder.Services.AddCors();

builder.Services.Configure<OllamaConfig>(
    builder.Configuration.GetSection(OllamaConfig.SectionName));
builder.Services.Configure<ChunkingConfig>(
    builder.Configuration.GetSection(ChunkingConfig.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
        o => o.UseVector()));

builder.Services.AddSingleton<FileParserFactory>();
builder.Services.AddScoped<SchemaInspector>();
builder.Services.AddScoped<SqlPromptBuilder>();
builder.Services.AddScoped<SqlValidator>();
builder.Services.AddScoped<NlToSqlService>();

var ollamaConfig = builder.Configuration.GetSection(OllamaConfig.SectionName).Get<OllamaConfig>()!;

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaTextEmbeddingGeneration(ollamaConfig.EmbeddingModel, new Uri(ollamaConfig.Endpoint));
kernelBuilder.AddOllamaChatCompletion(ollamaConfig.ChatModel, new Uri(ollamaConfig.Endpoint));

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel.GetRequiredService<ITextEmbeddingGenerationService>());
builder.Services.AddSingleton(kernel.GetRequiredService<IChatCompletionService>());

builder.Services.AddScoped<IRagService, RagService>();

var app = builder.Build();

// CORS for Angular UI (localhost:4200 in dev, ragui in Docker)
app.UseCors(policy => policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
}

var documentsGroup = app.MapGroup("/documents").WithOpenApi();
documentsGroup.MapPost("/upload", RagAndAI.Api.Features.Documents.UploadEndpoint.Handle)
    .WithName("UploadDocument")
    .WithSummary("Upload document to library")
    .Accepts<IFormFile>("multipart/form-data")
    .Produces<DocumentUploadResponse>(StatusCodes.Status200OK)
    .DisableAntiforgery();
documentsGroup.MapGet("/", RagAndAI.Api.Features.Documents.ListEndpoint.Handle)
    .WithName("ListDocuments")
    .WithSummary("List all library documents")
    .Produces<List<DocumentListResponse>>(StatusCodes.Status200OK);
documentsGroup.MapDelete("/{id}", RagAndAI.Api.Features.Documents.DeleteEndpoint.Handle)
    .WithName("DeleteDocument")
    .WithSummary("Delete document and its vector chunks")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

var chatGroup = app.MapGroup("/chat").WithOpenApi();
chatGroup.MapPost("/", QueryEndpoint.Handle)
    .WithName("ChatQuery")
    .WithSummary("Query library documents with question")
    .Produces<ChatQueryResponse>(StatusCodes.Status200OK);

var sessionsGroup = app.MapGroup("/sessions").WithOpenApi();
sessionsGroup.MapPost("/", RagAndAI.Api.Features.Sessions.CreateEndpoint.Handle)
    .WithName("CreateSession")
    .WithSummary("Create empty chat session")
    .Produces<RagAndAI.Api.Features.Sessions.SessionResponse>(StatusCodes.Status200OK);
sessionsGroup.MapGet("/", RagAndAI.Api.Features.Sessions.ListEndpoint.Handle)
    .WithName("ListSessions")
    .WithSummary("List all sessions with summary")
    .Produces<List<RagAndAI.Api.Features.Sessions.SessionResponse>>(StatusCodes.Status200OK);
sessionsGroup.MapGet("/{id}", RagAndAI.Api.Features.Sessions.GetEndpoint.Handle)
    .WithName("GetSession")
    .WithSummary("Get session details, documents, and message history")
    .Produces<RagAndAI.Api.Features.Sessions.SessionDetailResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);
sessionsGroup.MapDelete("/{id}", RagAndAI.Api.Features.Sessions.DeleteEndpoint.Handle)
    .WithName("DeleteSession")
    .WithSummary("Delete session and cascade delete documents, chunks, messages")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);
sessionsGroup.MapPost("/{id}/upload", RagAndAI.Api.Features.Sessions.UploadEndpoint.Handle)
    .WithName("UploadSessionDocument")
    .WithSummary("Upload document scoped to session")
    .Accepts<IFormFile>("multipart/form-data")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status400BadRequest)
    .DisableAntiforgery();
sessionsGroup.MapPost("/{id}/chat", RagAndAI.Api.Features.Sessions.ChatEndpoint.Handle)
    .WithName("SessionChat")
    .WithSummary("Chat within session with conversation history")
    .Produces<RagAndAI.Api.Features.Sessions.SessionChatResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status400BadRequest);
sessionsGroup.MapPost("/{id}/library-chat", RagAndAI.Api.Features.Sessions.LibraryChatEndpoint.Handle)
    .WithName("LibraryChat")
    .WithSummary("Chat using all library documents as context")
    .Produces<RagAndAI.Api.Features.Sessions.SessionChatResponse>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status400BadRequest);

var sqlGroup = app.MapGroup("/sql").WithOpenApi();
sqlGroup.MapPost("/query", ExecuteEndpoint.Handle)
    .WithName("SqlQuery")
    .WithSummary("Execute natural language query against database")
    .Produces<SqlQueryResponse>(StatusCodes.Status200OK);
sqlGroup.MapGet("/schema", SchemaEndpoint.Handle)
    .WithName("GetSchema")
    .WithSummary("Get database schema for debugging")
    .Produces<SchemaResponse>(StatusCodes.Status200OK);

app.MapGet("/health", CheckEndpoint.Handle)
    .WithOpenApi()
    .WithName("HealthCheck")
    .WithSummary("Liveness probe")
    .Produces(StatusCodes.Status200OK);

app.Run();

public partial class Program { }
