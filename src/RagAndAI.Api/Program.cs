using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.FileParser;
using RagAndAI.Api.Services.Rag;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OllamaConfig>(
    builder.Configuration.GetSection(OllamaConfig.SectionName));
builder.Services.Configure<ChunkingConfig>(
    builder.Configuration.GetSection(ChunkingConfig.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<FileParserFactory>();

var ollamaConfig = builder.Configuration.GetSection(OllamaConfig.SectionName).Get<OllamaConfig>()!;
var connectionString = builder.Configuration.GetConnectionString("Postgres")!;

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaTextEmbeddingGeneration(ollamaConfig.EmbeddingModel, new Uri(ollamaConfig.Endpoint));
kernelBuilder.AddOllamaChatCompletion(ollamaConfig.ChatModel, new Uri(ollamaConfig.Endpoint));
kernelBuilder.AddPostgresVectorStore(connectionString);

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(kernel.GetRequiredService<ITextEmbeddingGenerationService>());
builder.Services.AddSingleton(kernel.GetRequiredService<IChatCompletionService>());
builder.Services.AddSingleton(
    kernel.GetRequiredService<IVectorStore>()
          .GetCollection<Guid, DocumentChunkRecord>("document_chunks"));

builder.Services.AddScoped<IRagService, RagService>();

var app = builder.Build();
app.Run();
