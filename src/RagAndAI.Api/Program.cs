using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data;
using RagAndAI.Api.Services.FileParser;
using RagAndAI.Api.Services.Rag;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OllamaConfig>(
    builder.Configuration.GetSection(OllamaConfig.SectionName));
builder.Services.Configure<ChunkingConfig>(
    builder.Configuration.GetSection(ChunkingConfig.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"),
        o => o.UseVector()));

builder.Services.AddSingleton<FileParserFactory>();

var ollamaConfig = builder.Configuration.GetSection(OllamaConfig.SectionName).Get<OllamaConfig>()!;

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaTextEmbeddingGeneration(ollamaConfig.EmbeddingModel, new Uri(ollamaConfig.Endpoint));
kernelBuilder.AddOllamaChatCompletion(ollamaConfig.ChatModel, new Uri(ollamaConfig.Endpoint));

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel.GetRequiredService<ITextEmbeddingGenerationService>());
builder.Services.AddSingleton(kernel.GetRequiredService<IChatCompletionService>());

builder.Services.AddScoped<IRagService, RagService>();

var app = builder.Build();
app.Run();
