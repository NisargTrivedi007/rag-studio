using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data;
using RagAndAI.Api.Services.FileParser;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OllamaConfig>(
    builder.Configuration.GetSection(OllamaConfig.SectionName));
builder.Services.Configure<ChunkingConfig>(
    builder.Configuration.GetSection(ChunkingConfig.SectionName));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<FileParserFactory>();

var app = builder.Build();
app.Run();
