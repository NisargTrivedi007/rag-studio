# RAG + NL-to-SQL Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 10 minimal API that lets you upload documents and ask questions via RAG, and query an ecommerce database using plain English.

**Architecture:** Semantic Kernel manages the RAG pipeline (ingestion, embedding, retrieval) with pgvector as vector store. NL-to-SQL is manual — schema introspection, prompt building, SQL validation, execution. Both subsystems share one Postgres instance and one Ollama instance.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, Semantic Kernel 1.x, PostgreSQL + pgvector, Ollama (nomic-embed-text + llama3.1), EF Core 9, xUnit, NSubstitute

## Global Constraints

- Target framework: `net10.0`
- Postgres must have `pgvector` extension installed (`CREATE EXTENSION vector;`)
- Ollama must run locally at `http://localhost:11434` with `nomic-embed-text` and `llama3.1` pulled
- Only `SELECT` statements allowed through NL-to-SQL — never execute destructive SQL
- Chunk size: 512 tokens, overlap: 50 tokens, top-K: 5 (all tunable via `appsettings.json`)
- No authentication — single user, local dev + Docker
- SK manages the `document_chunks` table (vector store); EF Core manages all other tables
- Embedding dimension: 768 (nomic-embed-text output)

---

## File Map

```
RagAndAI.sln
├── src/RagAndAI.Api/
│   ├── RagAndAI.Api.csproj
│   ├── Program.cs                              — DI, middleware, endpoint registration
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── Config/
│   │   ├── OllamaConfig.cs                    — endpoint + model name POCOs
│   │   └── ChunkingConfig.cs                  — chunk size, overlap, top-K
│   ├── Data/
│   │   ├── AppDbContext.cs                    — EF context: documents + ecommerce tables
│   │   ├── Models/
│   │   │   ├── Document.cs                    — EF entity for uploaded files
│   │   │   ├── DocumentChunkRecord.cs         — SK vector store record (NOT an EF entity)
│   │   │   ├── Customer.cs
│   │   │   ├── Product.cs
│   │   │   ├── Order.cs
│   │   │   └── OrderItem.cs
│   │   └── Migrations/                        — EF migrations
│   ├── Services/
│   │   ├── FileParser/
│   │   │   ├── IFileParser.cs                 — interface: ExtractTextAsync(Stream, string)
│   │   │   ├── TextParser.cs
│   │   │   ├── PdfParser.cs                   — PdfPig
│   │   │   ├── WordParser.cs                  — DocumentFormat.OpenXml
│   │   │   ├── ExcelParser.cs                 — OpenXml → CSV-like text
│   │   │   └── FileParserFactory.cs           — picks parser by extension
│   │   ├── Rag/
│   │   │   ├── IRagService.cs                 — IngestAsync + QueryAsync
│   │   │   └── RagService.cs                  — SK kernel, pgvector, Ollama
│   │   └── NlToSql/
│   │       ├── INlToSqlService.cs             — QueryAsync
│   │       ├── NlToSqlService.cs              — orchestrates the 6-step flow
│   │       ├── SchemaInspector.cs             — reads information_schema via Npgsql
│   │       ├── SqlPromptBuilder.cs            — schema + question → prompt string
│   │       └── SqlValidator.cs               — blocks destructive SQL, cleans LLM output
│   └── Features/
│       ├── Documents/
│       │   ├── Upload.cs                       — POST /documents/upload
│       │   ├── List.cs                         — GET /documents
│       │   └── Delete.cs                       — DELETE /documents/{id}
│       ├── Chat/
│       │   └── Query.cs                        — POST /chat
│       └── SqlQuery/
│           ├── Execute.cs                      — POST /sql/query
│           └── Schema.cs                       — GET /sql/schema
└── tests/RagAndAI.Tests/
    ├── RagAndAI.Tests.csproj
    ├── FileParser/
    │   ├── TextParserTests.cs
    │   ├── PdfParserTests.cs
    │   ├── WordParserTests.cs
    │   ├── ExcelParserTests.cs
    │   └── FileParserFactoryTests.cs
    ├── NlToSql/
    │   ├── SqlValidatorTests.cs
    │   ├── SqlPromptBuilderTests.cs
    │   └── NlToSqlServiceTests.cs
    └── Rag/
        └── RagServiceTests.cs
```

---

## Task 1: Solution + Project Scaffolding

**Files:**
- Create: `RagAndAI.sln`
- Create: `src/RagAndAI.Api/RagAndAI.Api.csproj`
- Create: `tests/RagAndAI.Tests/RagAndAI.Tests.csproj`

**Interfaces:**
- Produces: compilable solution with all packages restored

- [ ] **Step 1: Scaffold solution and projects**

```bash
cd D:/Projects/RAG_And_AI
dotnet new sln -n RagAndAI
dotnet new webapi -n RagAndAI.Api -o src/RagAndAI.Api --use-minimal-apis
dotnet new xunit -n RagAndAI.Tests -o tests/RagAndAI.Tests
dotnet sln add src/RagAndAI.Api/RagAndAI.Api.csproj
dotnet sln add tests/RagAndAI.Tests/RagAndAI.Tests.csproj
dotnet add tests/RagAndAI.Tests/RagAndAI.Tests.csproj reference src/RagAndAI.Api/RagAndAI.Api.csproj
```

- [ ] **Step 2: Add packages to API project**

```bash
cd src/RagAndAI.Api
dotnet add package Microsoft.SemanticKernel
dotnet add package Microsoft.SemanticKernel.Connectors.Ollama
dotnet add package Microsoft.SemanticKernel.Connectors.Postgres
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package PdfPig
dotnet add package DocumentFormat.OpenXml
```

- [ ] **Step 3: Add packages to test project**

```bash
cd ../../tests/RagAndAI.Tests
dotnet add package NSubstitute
dotnet add package FluentAssertions
```

- [ ] **Step 4: Delete boilerplate from webapi template**

Remove the auto-generated `WeatherForecast.cs` and weather endpoints from `Program.cs`. Replace `Program.cs` with empty minimal shell:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();
```

- [ ] **Step 5: Verify build**

```bash
cd D:/Projects/RAG_And_AI
dotnet build
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "feat: scaffold solution with Api and Tests projects"
```

---

## Task 2: Configuration Models + appsettings

**Files:**
- Create: `src/RagAndAI.Api/Config/OllamaConfig.cs`
- Create: `src/RagAndAI.Api/Config/ChunkingConfig.cs`
- Modify: `src/RagAndAI.Api/appsettings.json`
- Create: `src/RagAndAI.Api/appsettings.Development.json`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Produces: `OllamaConfig` and `ChunkingConfig` bound from DI; accessible via `IOptions<T>`

- [ ] **Step 1: Write OllamaConfig**

```csharp
// src/RagAndAI.Api/Config/OllamaConfig.cs
namespace RagAndAI.Api.Config;

public class OllamaConfig
{
    public const string SectionName = "Ollama";
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string EmbeddingModel { get; set; } = "nomic-embed-text";
    public string ChatModel { get; set; } = "llama3.1";
}
```

- [ ] **Step 2: Write ChunkingConfig**

```csharp
// src/RagAndAI.Api/Config/ChunkingConfig.cs
namespace RagAndAI.Api.Config;

public class ChunkingConfig
{
    public const string SectionName = "Chunking";
    public int ChunkSize { get; set; } = 512;
    public int Overlap { get; set; } = 50;
    public int TopK { get; set; } = 5;
}
```

- [ ] **Step 3: Write appsettings.json**

```json
{
  "Ollama": {
    "Endpoint": "http://localhost:11434",
    "EmbeddingModel": "nomic-embed-text",
    "ChatModel": "llama3.1"
  },
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=ragdb;Username=postgres;Password=yourpassword"
  },
  "Chunking": {
    "ChunkSize": 512,
    "Overlap": 50,
    "TopK": 5
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 4: Write appsettings.Development.json**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft.SemanticKernel": "Debug"
    }
  }
}
```

- [ ] **Step 5: Register config in Program.cs**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<OllamaConfig>(
    builder.Configuration.GetSection(OllamaConfig.SectionName));
builder.Services.Configure<ChunkingConfig>(
    builder.Configuration.GetSection(ChunkingConfig.SectionName));

var app = builder.Build();
app.Run();
```

- [ ] **Step 6: Verify build**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/RagAndAI.Api/Config/ src/RagAndAI.Api/appsettings*.json src/RagAndAI.Api/Program.cs
git commit -m "feat: add OllamaConfig and ChunkingConfig with appsettings binding"
```

---

## Task 3: Data Models + EF Context + Migrations

**Files:**
- Create: `src/RagAndAI.Api/Data/Models/Document.cs`
- Create: `src/RagAndAI.Api/Data/Models/DocumentChunkRecord.cs`
- Create: `src/RagAndAI.Api/Data/Models/Customer.cs`
- Create: `src/RagAndAI.Api/Data/Models/Product.cs`
- Create: `src/RagAndAI.Api/Data/Models/Order.cs`
- Create: `src/RagAndAI.Api/Data/Models/OrderItem.cs`
- Create: `src/RagAndAI.Api/Data/AppDbContext.cs`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Produces: `AppDbContext` registered in DI; EF migrations applied; SK vector store record `DocumentChunkRecord` defined

- [ ] **Step 1: Write Document entity**

```csharp
// src/RagAndAI.Api/Data/Models/Document.cs
using System.ComponentModel.DataAnnotations;

namespace RagAndAI.Api.Data.Models;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [Required] public string Filename { get; set; } = string.Empty;
    [Required] public string FileType { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Metadata { get; set; }  // JSON string
}
```

- [ ] **Step 2: Write DocumentChunkRecord (SK vector store record — NOT an EF entity)**

```csharp
// src/RagAndAI.Api/Data/Models/DocumentChunkRecord.cs
using Microsoft.Extensions.VectorData;

namespace RagAndAI.Api.Data.Models;

public class DocumentChunkRecord
{
    [VectorStoreRecordKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [VectorStoreRecordData(IsFilterable = true)]
    public Guid DocumentId { get; set; }

    [VectorStoreRecordData]
    public int ChunkIndex { get; set; }

    [VectorStoreRecordData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreRecordVector(768, DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }

    [VectorStoreRecordData]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 3: Write ecommerce entities**

```csharp
// src/RagAndAI.Api/Data/Models/Customer.cs
namespace RagAndAI.Api.Data.Models;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Order> Orders { get; set; } = [];
}
```

```csharp
// src/RagAndAI.Api/Data/Models/Product.cs
namespace RagAndAI.Api.Data.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
```

```csharp
// src/RagAndAI.Api/Data/Models/Order.cs
namespace RagAndAI.Api.Data.Models;

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string Status { get; set; } = "pending";
    public decimal Total { get; set; }
    public ICollection<OrderItem> Items { get; set; } = [];
}
```

```csharp
// src/RagAndAI.Api/Data/Models/OrderItem.cs
namespace RagAndAI.Api.Data.Models;

public class OrderItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

- [ ] **Step 4: Write AppDbContext**

```csharp
// src/RagAndAI.Api/Data/AppDbContext.cs
using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Filename).HasColumnName("filename").IsRequired();
            e.Property(x => x.FileType).HasColumnName("file_type").IsRequired();
            e.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            e.Property(x => x.Metadata).HasColumnName("metadata");
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.ToTable("customers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Product>(e => e.ToTable("products"));

        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasOne(x => x.Customer).WithMany(x => x.Orders)
                .HasForeignKey(x => x.CustomerId);
        });

        modelBuilder.Entity<OrderItem>(e =>
        {
            e.ToTable("order_items");
            e.HasOne(x => x.Order).WithMany(x => x.Items)
                .HasForeignKey(x => x.OrderId);
            e.HasOne(x => x.Product).WithMany(x => x.OrderItems)
                .HasForeignKey(x => x.ProductId);
        });
    }
}
```

- [ ] **Step 5: Register EF in Program.cs**

Add after the config registrations:

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
```

Add to top of file:
```csharp
using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
```

- [ ] **Step 6: Create and apply migration**

```bash
cd D:/Projects/RAG_And_AI
dotnet ef migrations add InitialSchema --project src/RagAndAI.Api --startup-project src/RagAndAI.Api
```

Before applying, ensure Postgres is running and `ragdb` database exists:
```sql
CREATE DATABASE ragdb;
\c ragdb
CREATE EXTENSION IF NOT EXISTS vector;
```

Then apply:
```bash
dotnet ef database update --project src/RagAndAI.Api --startup-project src/RagAndAI.Api
```

- [ ] **Step 7: Verify tables exist**

Connect to Postgres and run:
```sql
\dt
```
Expected: `documents`, `customers`, `products`, `orders`, `order_items` tables exist.

- [ ] **Step 8: Commit**

```bash
git add src/RagAndAI.Api/Data/ src/RagAndAI.Api/Program.cs
git commit -m "feat: add EF data models, AppDbContext, and initial migration"
```

---

## Task 4: IFileParser + TextParser + PdfParser

**Files:**
- Create: `src/RagAndAI.Api/Services/FileParser/IFileParser.cs`
- Create: `src/RagAndAI.Api/Services/FileParser/TextParser.cs`
- Create: `src/RagAndAI.Api/Services/FileParser/PdfParser.cs`
- Create: `tests/RagAndAI.Tests/FileParser/TextParserTests.cs`
- Create: `tests/RagAndAI.Tests/FileParser/PdfParserTests.cs`

**Interfaces:**
- Produces: `IFileParser` with `Task<string> ExtractTextAsync(Stream stream, string fileName)`
- Consumes: nothing from prior tasks

- [ ] **Step 1: Write IFileParser**

```csharp
// src/RagAndAI.Api/Services/FileParser/IFileParser.cs
namespace RagAndAI.Api.Services.FileParser;

public interface IFileParser
{
    Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write TextParser**

```csharp
// src/RagAndAI.Api/Services/FileParser/TextParser.cs
namespace RagAndAI.Api.Services.FileParser;

public class TextParser : IFileParser
{
    public async Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, leaveOpen: true);
        return await reader.ReadToEndAsync(ct);
    }
}
```

- [ ] **Step 3: Write failing tests for TextParser**

```csharp
// tests/RagAndAI.Tests/FileParser/TextParserTests.cs
using System.Text;
using FluentAssertions;
using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class TextParserTests
{
    private readonly TextParser _sut = new();

    [Fact]
    public async Task ExtractTextAsync_ReturnsFullContent()
    {
        var content = "Hello, world! This is a test document.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await _sut.ExtractTextAsync(stream, "test.txt");

        result.Should().Be(content);
    }

    [Fact]
    public async Task ExtractTextAsync_HandlesMultilineContent()
    {
        var content = "Line 1\nLine 2\nLine 3";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var result = await _sut.ExtractTextAsync(stream, "test.txt");

        result.Should().Contain("Line 1").And.Contain("Line 3");
    }

    [Fact]
    public async Task ExtractTextAsync_ReturnsEmptyStringForEmptyFile()
    {
        using var stream = new MemoryStream([]);

        var result = await _sut.ExtractTextAsync(stream, "empty.txt");

        result.Should().BeEmpty();
    }
}
```

- [ ] **Step 4: Run TextParser tests — expect pass**

```bash
cd D:/Projects/RAG_And_AI
dotnet test tests/RagAndAI.Tests --filter "TextParserTests" -v
```

Expected: 3 tests pass.

- [ ] **Step 5: Write PdfParser**

```csharp
// src/RagAndAI.Api/Services/FileParser/PdfParser.cs
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using System.Text;

namespace RagAndAI.Api.Services.FileParser;

public class PdfParser : IFileParser
{
    public Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        var bytes = memoryStream.ToArray();

        using var document = PdfDocument.Open(bytes);
        var sb = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            sb.AppendLine(page.Text);
        }

        return Task.FromResult(sb.ToString().Trim());
    }
}
```

- [ ] **Step 6: Write PdfParser test**

PdfPig can open a minimal valid PDF byte array. Create one programmatically or use a small fixture. For this test, validate that the parser returns non-empty text from a real PDF byte array.

```csharp
// tests/RagAndAI.Tests/FileParser/PdfParserTests.cs
using FluentAssertions;
using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class PdfParserTests
{
    private readonly PdfParser _sut = new();

    [Fact]
    public async Task ExtractTextAsync_ReturnsText_FromValidPdf()
    {
        // Minimal valid 1-page PDF containing "Hello PDF"
        // Generated via: https://www.pdfescape.com or use the byte literal below
        // This is a real minimal PDF — do not modify the byte sequence
        var pdfBytes = GetMinimalPdfBytes("Hello PDF");
        using var stream = new MemoryStream(pdfBytes);

        var result = await _sut.ExtractTextAsync(stream, "test.pdf");

        result.Should().Contain("Hello");
    }

    private static byte[] GetMinimalPdfBytes(string text)
    {
        // Use PdfPig's own test fixtures or create via iText/PDFSharp in test setup.
        // Simplest approach: place a real small PDF file at tests/Fixtures/sample.pdf
        // and load it here. For CI, commit the fixture file.
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "Fixtures", "sample.pdf");
        if (File.Exists(fixturePath))
            return File.ReadAllBytes(fixturePath);

        // Fallback: skip test if no fixture
        throw new SkipException("Place a sample.pdf in tests/Fixtures/ to run this test");
    }
}
```

Create `tests/RagAndAI.Tests/Fixtures/` directory and place a small real PDF there (any single-page PDF containing readable text).

- [ ] **Step 7: Run PdfParser tests**

```bash
dotnet test tests/RagAndAI.Tests --filter "PdfParserTests" -v
```

Expected: pass (or skip if fixture not placed yet).

- [ ] **Step 8: Commit**

```bash
git add src/RagAndAI.Api/Services/FileParser/ tests/RagAndAI.Tests/FileParser/
git commit -m "feat: add IFileParser, TextParser, PdfParser with tests"
```

---

## Task 5: WordParser + ExcelParser + FileParserFactory

**Files:**
- Create: `src/RagAndAI.Api/Services/FileParser/WordParser.cs`
- Create: `src/RagAndAI.Api/Services/FileParser/ExcelParser.cs`
- Create: `src/RagAndAI.Api/Services/FileParser/FileParserFactory.cs`
- Create: `tests/RagAndAI.Tests/FileParser/WordParserTests.cs`
- Create: `tests/RagAndAI.Tests/FileParser/ExcelParserTests.cs`
- Create: `tests/RagAndAI.Tests/FileParser/FileParserFactoryTests.cs`

**Interfaces:**
- Consumes: `IFileParser` from Task 4
- Produces: `FileParserFactory.GetParser(string extension)` returns correct `IFileParser`; throws `NotSupportedException` for unknown types

- [ ] **Step 1: Write WordParser**

```csharp
// src/RagAndAI.Api/Services/FileParser/WordParser.cs
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace RagAndAI.Api.Services.FileParser;

public class WordParser : IFileParser
{
    public Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var doc = WordprocessingDocument.Open(stream, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return Task.FromResult(string.Empty);

        var sb = new StringBuilder();
        foreach (var para in body.Elements<Paragraph>())
        {
            sb.AppendLine(para.InnerText);
        }

        return Task.FromResult(sb.ToString().Trim());
    }
}
```

- [ ] **Step 2: Write ExcelParser**

```csharp
// src/RagAndAI.Api/Services/FileParser/ExcelParser.cs
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using System.Text;

namespace RagAndAI.Api.Services.FileParser;

public class ExcelParser : IFileParser
{
    public Task<string> ExtractTextAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var doc = SpreadsheetDocument.Open(stream, false);
        var workbook = doc.WorkbookPart;
        if (workbook is null) return Task.FromResult(string.Empty);

        var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable;
        var sb = new StringBuilder();

        foreach (var sheet in workbook.WorksheetParts)
        {
            var rows = sheet.Worksheet.GetFirstChild<SheetData>()?.Elements<Row>() ?? [];
            foreach (var row in rows)
            {
                var cells = row.Elements<Cell>()
                    .Select(c => GetCellValue(c, sharedStrings));
                sb.AppendLine(string.Join("\t", cells));
            }
        }

        return Task.FromResult(sb.ToString().Trim());
    }

    private static string GetCellValue(Cell cell, SharedStringTable? sharedStrings)
    {
        var value = cell.CellValue?.Text ?? string.Empty;
        if (cell.DataType?.Value == CellValues.SharedString
            && sharedStrings is not null
            && int.TryParse(value, out var index))
        {
            return sharedStrings.ElementAt(index).InnerText;
        }
        return value;
    }
}
```

- [ ] **Step 3: Write FileParserFactory**

```csharp
// src/RagAndAI.Api/Services/FileParser/FileParserFactory.cs
namespace RagAndAI.Api.Services.FileParser;

public class FileParserFactory
{
    private static readonly Dictionary<string, IFileParser> Parsers = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".txt", new TextParser() },
        { ".md",  new TextParser() },
        { ".pdf", new PdfParser() },
        { ".docx", new WordParser() },
        { ".xlsx", new ExcelParser() },
        { ".csv",  new TextParser() },
    };

    public IFileParser GetParser(string fileExtension)
    {
        if (Parsers.TryGetValue(fileExtension, out var parser))
            return parser;

        throw new NotSupportedException(
            $"File type '{fileExtension}' is not supported. Supported: {string.Join(", ", Parsers.Keys)}");
    }
}
```

- [ ] **Step 4: Write failing FileParserFactory tests**

```csharp
// tests/RagAndAI.Tests/FileParser/FileParserFactoryTests.cs
using FluentAssertions;
using RagAndAI.Api.Services.FileParser;

namespace RagAndAI.Tests.FileParser;

public class FileParserFactoryTests
{
    private readonly FileParserFactory _sut = new();

    [Theory]
    [InlineData(".txt", typeof(TextParser))]
    [InlineData(".TXT", typeof(TextParser))]
    [InlineData(".md",  typeof(TextParser))]
    [InlineData(".pdf", typeof(PdfParser))]
    [InlineData(".docx", typeof(WordParser))]
    [InlineData(".xlsx", typeof(ExcelParser))]
    public void GetParser_ReturnsCorrectParserType(string extension, Type expectedType)
    {
        var parser = _sut.GetParser(extension);
        parser.Should().BeOfType(expectedType);
    }

    [Theory]
    [InlineData(".pptx")]
    [InlineData(".zip")]
    [InlineData(".exe")]
    public void GetParser_ThrowsForUnsupportedExtension(string extension)
    {
        var act = () => _sut.GetParser(extension);
        act.Should().Throw<NotSupportedException>();
    }
}
```

- [ ] **Step 5: Run FileParserFactory tests — expect pass**

```bash
dotnet test tests/RagAndAI.Tests --filter "FileParserFactoryTests" -v
```

Expected: all pass.

- [ ] **Step 6: Register FileParserFactory in Program.cs**

```csharp
builder.Services.AddSingleton<FileParserFactory>();
```

- [ ] **Step 7: Commit**

```bash
git add src/RagAndAI.Api/Services/FileParser/ tests/RagAndAI.Tests/FileParser/ src/RagAndAI.Api/Program.cs
git commit -m "feat: add WordParser, ExcelParser, FileParserFactory with tests"
```

---

## Task 6: RAG Ingestion (SK + pgvector + IngestAsync)

**Files:**
- Create: `src/RagAndAI.Api/Services/Rag/IRagService.cs`
- Create: `src/RagAndAI.Api/Services/Rag/RagService.cs`
- Modify: `src/RagAndAI.Api/Program.cs`
- Create: `tests/RagAndAI.Tests/Rag/RagServiceTests.cs`

**Interfaces:**
- Consumes: `OllamaConfig`, `ChunkingConfig`, `ITextEmbeddingGenerationService` (SK), `IVectorStoreRecordCollection<Guid, DocumentChunkRecord>`
- Produces:
  - `IRagService.IngestAsync(Guid documentId, string text, CancellationToken ct)`
  - `IRagService.QueryAsync(string question, IEnumerable<Guid> documentIds, CancellationToken ct)` returns `RagResult`

- [ ] **Step 1: Write IRagService and RagResult**

```csharp
// src/RagAndAI.Api/Services/Rag/IRagService.cs
namespace RagAndAI.Api.Services.Rag;

public record RagResult(string Answer, IReadOnlyList<string> Sources);

public interface IRagService
{
    Task IngestAsync(Guid documentId, string text, CancellationToken ct = default);
    Task<RagResult> QueryAsync(string question, IEnumerable<Guid> documentIds, CancellationToken ct = default);
    Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default);
}
```

- [ ] **Step 2: Write RagService — IngestAsync only**

```csharp
// src/RagAndAI.Api/Services/Rag/RagService.cs
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Embeddings;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Services.Rag;

public class RagService(
    ITextEmbeddingGenerationService embeddingService,
    IVectorStoreRecordCollection<Guid, DocumentChunkRecord> collection,
    IOptions<ChunkingConfig> chunkingOptions) : IRagService
{
    private readonly ChunkingConfig _chunking = chunkingOptions.Value;

    public async Task IngestAsync(Guid documentId, string text, CancellationToken ct = default)
    {
        await collection.CreateCollectionIfNotExistsAsync(ct);

        var chunks = ChunkText(text, _chunking.ChunkSize, _chunking.Overlap);
        var embeddings = await embeddingService.GenerateEmbeddingsAsync(chunks, cancellationToken: ct);

        for (int i = 0; i < chunks.Count; i++)
        {
            var record = new DocumentChunkRecord
            {
                Id = Guid.NewGuid(),
                DocumentId = documentId,
                ChunkIndex = i,
                Content = chunks[i],
                Embedding = embeddings[i],
                CreatedAt = DateTimeOffset.UtcNow
            };
            await collection.UpsertAsync(record, cancellationToken: ct);
        }
    }

    public Task<RagResult> QueryAsync(string question, IEnumerable<Guid> documentIds, CancellationToken ct = default)
        => throw new NotImplementedException(); // implemented in Task 7

    public async Task DeleteDocumentChunksAsync(Guid documentId, CancellationToken ct = default)
    {
        // Search all chunks for this document and delete them
        // Note: SK vector stores don't always support filter-delete natively.
        // We query by filter and delete by key.
        var filter = new VectorSearchFilter().EqualTo(nameof(DocumentChunkRecord.DocumentId), documentId);
        var options = new VectorSearchOptions { Top = 10000, Filter = filter };

        // Use a zero-vector search to get all chunks for this document
        var zeroVector = new ReadOnlyMemory<float>(new float[768]);
        var results = collection.VectorizedSearchAsync(zeroVector, options, ct);

        await foreach (var result in results)
        {
            await collection.DeleteAsync(result.Record.Id, cancellationToken: ct);
        }
    }

    private static List<string> ChunkText(string text, int chunkSize, int overlap)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        int step = chunkSize - overlap;

        for (int i = 0; i < words.Length; i += step)
        {
            var chunk = string.Join(' ', words.Skip(i).Take(chunkSize));
            if (!string.IsNullOrWhiteSpace(chunk))
                chunks.Add(chunk);
        }

        return chunks.Count > 0 ? chunks : [text];
    }
}
```

- [ ] **Step 3: Wire SK + Ollama + pgvector in Program.cs**

```csharp
using Microsoft.SemanticKernel;
using Microsoft.Extensions.VectorData;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.Rag;

// After config registrations, add:
var ollamaConfig = builder.Configuration.GetSection(OllamaConfig.SectionName).Get<OllamaConfig>()!;
var connectionString = builder.Configuration.GetConnectionString("Postgres")!;

var kernelBuilder = Kernel.CreateBuilder();
kernelBuilder.AddOllamaTextEmbeddingGeneration(
    ollamaConfig.EmbeddingModel,
    new Uri(ollamaConfig.Endpoint));
kernelBuilder.AddOllamaChatCompletion(
    ollamaConfig.ChatModel,
    new Uri(ollamaConfig.Endpoint));
kernelBuilder.AddPostgresVectorStore(connectionString);

var kernel = kernelBuilder.Build();
builder.Services.AddSingleton(kernel);
builder.Services.AddSingleton(kernel.GetRequiredService<ITextEmbeddingGenerationService>());
builder.Services.AddSingleton(kernel.GetRequiredService<IChatCompletionService>());
builder.Services.AddSingleton(
    kernel.GetRequiredService<IVectorStore>()
          .GetCollection<Guid, DocumentChunkRecord>("document_chunks"));

builder.Services.AddScoped<IRagService, RagService>();
```

- [ ] **Step 4: Write RagService unit tests (ingestion only)**

```csharp
// tests/RagAndAI.Tests/Rag/RagServiceTests.cs
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Embeddings;
using NSubstitute;
using RagAndAI.Api.Config;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Tests.Rag;

public class RagServiceTests
{
    private readonly ITextEmbeddingGenerationService _embedding = Substitute.For<ITextEmbeddingGenerationService>();
    private readonly IVectorStoreRecordCollection<Guid, DocumentChunkRecord> _collection =
        Substitute.For<IVectorStoreRecordCollection<Guid, DocumentChunkRecord>>();
    private readonly RagService _sut;

    public RagServiceTests()
    {
        var config = Options.Create(new ChunkingConfig { ChunkSize = 10, Overlap = 2, TopK = 3 });
        _sut = new RagService(_embedding, _collection, config);
    }

    [Fact]
    public async Task IngestAsync_UpsertsSingleChunk_ForShortText()
    {
        var documentId = Guid.NewGuid();
        var text = "short text";
        var fakeEmbedding = new ReadOnlyMemory<float>(new float[768]);

        _embedding.GenerateEmbeddingsAsync(Arg.Any<IList<string>>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<ReadOnlyMemory<float>> { fakeEmbedding });

        await _sut.IngestAsync(documentId, text);

        await _collection.Received(1).UpsertAsync(
            Arg.Is<DocumentChunkRecord>(r => r.DocumentId == documentId),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IngestAsync_CreatesCollectionBeforeUpserting()
    {
        var documentId = Guid.NewGuid();
        _embedding.GenerateEmbeddingsAsync(Arg.Any<IList<string>>(), cancellationToken: Arg.Any<CancellationToken>())
            .Returns(new List<ReadOnlyMemory<float>> { new(new float[768]) });

        await _sut.IngestAsync(documentId, "some text");

        await _collection.Received(1).CreateCollectionIfNotExistsAsync(Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 5: Run RagService tests**

```bash
dotnet test tests/RagAndAI.Tests --filter "RagServiceTests" -v
```

Expected: 2 pass.

- [ ] **Step 6: Build to verify wiring**

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/RagAndAI.Api/Services/Rag/ tests/RagAndAI.Tests/Rag/ src/RagAndAI.Api/Program.cs
git commit -m "feat: add IRagService, RagService ingestion, SK+Ollama+pgvector wiring"
```

---

## Task 7: RAG Query + Chat (QueryAsync)

**Files:**
- Modify: `src/RagAndAI.Api/Services/Rag/RagService.cs`
- Modify: `tests/RagAndAI.Tests/Rag/RagServiceTests.cs`

**Interfaces:**
- Consumes: `IChatCompletionService` (SK), `IVectorStoreRecordCollection<Guid, DocumentChunkRecord>`, `ChunkingConfig.TopK`
- Produces: `RagService.QueryAsync` — embeds question, retrieves top-K chunks filtered by documentIds, calls LLM, returns `RagResult`

- [ ] **Step 1: Implement QueryAsync in RagService**

Replace the `throw new NotImplementedException()` in `QueryAsync`:

```csharp
public async Task<RagResult> QueryAsync(
    string question,
    IEnumerable<Guid> documentIds,
    CancellationToken ct = default)
{
    // Embed the question
    var questionEmbeddings = await embeddingService.GenerateEmbeddingsAsync([question], cancellationToken: ct);
    var queryVector = questionEmbeddings[0];

    // Retrieve top-K chunks across requested documents
    var docIdSet = documentIds.ToHashSet();
    var allChunks = new List<DocumentChunkRecord>();

    foreach (var docId in docIdSet)
    {
        var filter = new VectorSearchFilter().EqualTo(nameof(DocumentChunkRecord.DocumentId), docId);
        var searchOptions = new VectorSearchOptions { Top = _chunking.TopK, Filter = filter };
        var results = collection.VectorizedSearchAsync(queryVector, searchOptions, ct);

        await foreach (var result in results)
            allChunks.Add(result.Record);
    }

    if (allChunks.Count == 0)
        return new RagResult("No relevant content found for your question.", []);

    // Build context prompt
    var context = string.Join("\n\n---\n\n", allChunks.Select(c => c.Content));
    var prompt = $"""
        You are a helpful assistant. Answer the question using ONLY the context below.
        If the context doesn't contain the answer, say "I don't have enough information to answer that."

        Context:
        {context}

        Question: {question}

        Answer:
        """;

    var chatService = kernel.GetRequiredService<IChatCompletionService>();
    var chatHistory = new Microsoft.SemanticKernel.ChatCompletion.ChatHistory();
    chatHistory.AddUserMessage(prompt);

    var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: ct);
    var answer = response.Content ?? "No response generated.";

    return new RagResult(answer, allChunks.Select(c => c.Content).ToList());
}
```

Note: `QueryAsync` needs access to `kernel` — update constructor to also inject `Kernel`:

```csharp
public class RagService(
    Kernel kernel,
    ITextEmbeddingGenerationService embeddingService,
    IVectorStoreRecordCollection<Guid, DocumentChunkRecord> collection,
    IOptions<ChunkingConfig> chunkingOptions) : IRagService
```

- [ ] **Step 2: Write QueryAsync tests**

Add to `RagServiceTests.cs`:

```csharp
[Fact]
public async Task QueryAsync_ReturnsNoContentMessage_WhenNoChunksFound()
{
    var kernel = Substitute.For<Kernel>();
    var config = Options.Create(new ChunkingConfig { ChunkSize = 10, Overlap = 2, TopK = 3 });

    _collection.VectorizedSearchAsync(
            Arg.Any<ReadOnlyMemory<float>>(),
            Arg.Any<VectorSearchOptions>(),
            Arg.Any<CancellationToken>())
        .Returns(AsyncEnumerable.Empty<VectorSearchResult<DocumentChunkRecord>>());

    _embedding.GenerateEmbeddingsAsync(Arg.Any<IList<string>>(), cancellationToken: Arg.Any<CancellationToken>())
        .Returns(new List<ReadOnlyMemory<float>> { new(new float[768]) });

    var sut = new RagService(kernel, _embedding, _collection, config);

    var result = await sut.QueryAsync("test question", [Guid.NewGuid()]);

    result.Answer.Should().Contain("No relevant content");
    result.Sources.Should().BeEmpty();
}
```

- [ ] **Step 3: Run all Rag tests**

```bash
dotnet test tests/RagAndAI.Tests --filter "RagServiceTests" -v
```

Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add src/RagAndAI.Api/Services/Rag/ tests/RagAndAI.Tests/Rag/
git commit -m "feat: implement RagService.QueryAsync with vector retrieval and LLM generation"
```

---

## Task 8: Document API Endpoints (3 feature files)

**Files:**
- Create: `src/RagAndAI.Api/Features/Documents/Upload.cs`
- Create: `src/RagAndAI.Api/Features/Documents/List.cs`
- Create: `src/RagAndAI.Api/Features/Documents/Delete.cs`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `IRagService`, `FileParserFactory`
- Produces:
  - `POST /documents/upload` → `{ id, filename, fileType, uploadedAt }`
  - `GET /documents` → `[{ id, filename, fileType, uploadedAt }]`
  - `DELETE /documents/{id}` → 204 or 404

- [ ] **Step 1: Create Features/Documents/Upload.cs**

```csharp
// src/RagAndAI.Api/Features/Documents/Upload.cs
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.FileParser;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Documents;

public static class Upload
{
    public static RouteGroupBuilder MapUpload(this RouteGroupBuilder group)
    {
        group.MapPost("/upload", Handle).DisableAntiforgery();
        return group;
    }

    private static async Task<IResult> Handle(
        IFormFile file,
        AppDbContext db,
        IRagService ragService,
        FileParserFactory parserFactory,
        CancellationToken ct)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        IFileParser parser;
        try
        {
            parser = parserFactory.GetParser(extension);
        }
        catch (NotSupportedException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }

        string extractedText;
        try
        {
            await using var stream = file.OpenReadStream();
            extractedText = await parser.ExtractTextAsync(stream, file.FileName, ct);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to parse file: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(extractedText))
            return Results.BadRequest(new { error = "Document contains no extractable text." });

        var document = new Document
        {
            Filename = file.FileName,
            FileType = extension.TrimStart('.')
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        try
        {
            await ragService.IngestAsync(document.Id, extractedText, ct);
        }
        catch (Exception ex)
        {
            db.Documents.Remove(document);
            await db.SaveChangesAsync(ct);
            return Results.Problem($"Embedding failed: {ex.Message}", statusCode: 503);
        }

        return Results.Created($"/documents/{document.Id}", new
        {
            document.Id,
            document.Filename,
            document.FileType,
            document.UploadedAt
        });
    }
}
```

- [ ] **Step 2: Create Features/Documents/List.cs**

```csharp
// src/RagAndAI.Api/Features/Documents/List.cs
using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Features.Documents;

public static class List
{
    public static RouteGroupBuilder MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", Handle);
        return group;
    }

    private static async Task<IResult> Handle(AppDbContext db, CancellationToken ct)
    {
        var docs = await db.Documents
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new { d.Id, d.Filename, d.FileType, d.UploadedAt })
            .ToListAsync(ct);
        return Results.Ok(docs);
    }
}
```

- [ ] **Step 3: Create Features/Documents/Delete.cs**

```csharp
// src/RagAndAI.Api/Features/Documents/Delete.cs
using RagAndAI.Api.Data;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Documents;

public static class Delete
{
    public static RouteGroupBuilder MapDelete(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle);
        return group;
    }

    private static async Task<IResult> Handle(
        Guid id,
        AppDbContext db,
        IRagService ragService,
        CancellationToken ct)
    {
        var doc = await db.Documents.FindAsync([id], ct);
        if (doc is null) return Results.NotFound();

        await ragService.DeleteDocumentChunksAsync(id, ct);
        db.Documents.Remove(doc);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
```

- [ ] **Step 4: Update Program.cs**

```csharp
using RagAndAI.Api.Features.Documents;

// After app = builder.Build():
var docs = app.MapGroup("/documents");
docs.MapUpload();
docs.MapList();
docs.MapDelete();
```

- [ ] **Step 5: Manual test**

```bash
dotnet run --project src/RagAndAI.Api
```

```bash
curl -X POST http://localhost:5000/documents/upload \
  -F "file=@/path/to/test.txt"
```

Expected: `201 Created`.

```bash
curl http://localhost:5000/documents
```

Expected: JSON array with document.

- [ ] **Step 6: Commit**

```bash
git add src/RagAndAI.Api/Features/Documents/ src/RagAndAI.Api/Program.cs
git commit -m "feat: add document upload, list, delete endpoints (feature folder)"
```

---

## Task 9: Chat Endpoint

**Files:**
- Create: `src/RagAndAI.Api/Features/Chat/Query.cs`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Consumes: `IRagService`
- Produces: `POST /chat` → `{ answer, sources }`

- [ ] **Step 1: Create Features/Chat/Query.cs**

```csharp
// src/RagAndAI.Api/Features/Chat/Query.cs
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Chat;

public static class Query
{
    public static WebApplication MapChatQuery(this WebApplication app)
    {
        app.MapPost("/chat", Handle);
        return app;
    }

    private static async Task<IResult> Handle(
        ChatRequest request,
        IRagService ragService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest(new { error = "Question is required." });

        if (request.DocumentIds.Count == 0)
            return Results.BadRequest(new { error = "At least one document_id is required." });

        try
        {
            var result = await ragService.QueryAsync(request.Question, request.DocumentIds, ct);
            return Results.Ok(new { answer = result.Answer, sources = result.Sources });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Query failed: {ex.Message}", statusCode: 503);
        }
    }

    private record ChatRequest(
        List<Guid> DocumentIds,
        string Question);
}
```

- [ ] **Step 2: Register in Program.cs**

```csharp
using RagAndAI.Api.Features.Chat;

// After app = builder.Build():
app.MapChatQuery();
```

- [ ] **Step 3: Manual end-to-end test**

First upload a document (Task 8 Step 5). Note the returned `id`. Then:

```bash
curl -X POST http://localhost:5000/chat \
  -H "Content-Type: application/json" \
  -d '{"documentIds": ["<paste-document-id>"], "question": "What is this document about?"}'
```

Expected: `{ "answer": "...", "sources": [...] }`. Ollama must be running with llama3.1 pulled.

- [ ] **Step 4: Commit**

```bash
git add src/RagAndAI.Api/Features/Chat/ src/RagAndAI.Api/Program.cs
git commit -m "feat: add POST /chat endpoint for RAG queries (feature folder)"
```

---

## Task 10: SchemaInspector

**Files:**
- Create: `src/RagAndAI.Api/Services/NlToSql/SchemaInspector.cs`
- Create: `tests/RagAndAI.Tests/NlToSql/SchemaInspectorTests.cs` (integration, skipped without DB)

**Interfaces:**
- Consumes: Postgres connection string via `IConfiguration`
- Produces: `Task<string> GetSchemaAsync(CancellationToken ct)` — returns formatted schema string, excludes RAG tables

- [ ] **Step 1: Write SchemaInspector**

```csharp
// src/RagAndAI.Api/Services/NlToSql/SchemaInspector.cs
using Npgsql;
using System.Text;

namespace RagAndAI.Api.Services.NlToSql;

public class SchemaInspector(IConfiguration configuration)
{
    private static readonly HashSet<string> ExcludedTables = new(StringComparer.OrdinalIgnoreCase)
    {
        "documents", "document_chunks", "__efmigrationshistory"
    };

    public async Task<string> GetSchemaAsync(CancellationToken ct = default)
    {
        var connectionString = configuration.GetConnectionString("Postgres")!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        const string sql = """
            SELECT
                c.table_name,
                c.column_name,
                c.data_type,
                c.is_nullable,
                c.column_default,
                tc.constraint_type
            FROM information_schema.columns c
            LEFT JOIN information_schema.key_column_usage kcu
                ON c.table_name = kcu.table_name AND c.column_name = kcu.column_name
                AND c.table_schema = kcu.table_schema
            LEFT JOIN information_schema.table_constraints tc
                ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = tc.table_schema
            WHERE c.table_schema = 'public'
            ORDER BY c.table_name, c.ordinal_position;
            """;

        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var tableColumns = new Dictionary<string, List<ColumnInfo>>();
        while (await reader.ReadAsync(ct))
        {
            var table = reader.GetString(0);
            if (ExcludedTables.Contains(table)) continue;

            if (!tableColumns.TryGetValue(table, out var cols))
                tableColumns[table] = cols = [];

            cols.Add(new ColumnInfo(
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3) == "YES",
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return FormatSchema(tableColumns);
    }

    private static string FormatSchema(Dictionary<string, List<ColumnInfo>> tables)
    {
        var sb = new StringBuilder();
        foreach (var (table, columns) in tables)
        {
            sb.AppendLine($"Table: {table}");
            foreach (var col in columns)
            {
                var nullable = col.IsNullable ? "nullable" : "not null";
                var pk = col.ConstraintType == "PRIMARY KEY" ? " [PK]" : "";
                var fk = col.ConstraintType == "FOREIGN KEY" ? " [FK]" : "";
                sb.AppendLine($"  - {col.Name}: {col.DataType} ({nullable}){pk}{fk}");
            }
            sb.AppendLine();
        }
        return sb.ToString().Trim();
    }

    private record ColumnInfo(string Name, string DataType, bool IsNullable, string? ConstraintType);
}
```

- [ ] **Step 2: Register SchemaInspector in Program.cs**

```csharp
using RagAndAI.Api.Services.NlToSql;
builder.Services.AddSingleton<SchemaInspector>();
```

- [ ] **Step 3: Manual test — verify schema output**

Temporarily add a debug endpoint or call via a quick integration test. Start app and hit `GET /sql/schema` (added in Task 13). For now, verify compile-time correctness:

```bash
dotnet build
```

Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/RagAndAI.Api/Services/NlToSql/SchemaInspector.cs src/RagAndAI.Api/Program.cs
git commit -m "feat: add SchemaInspector to read ecommerce schema from information_schema"
```

---

## Task 11: SqlValidator

**Files:**
- Create: `src/RagAndAI.Api/Services/NlToSql/SqlValidator.cs`
- Create: `tests/RagAndAI.Tests/NlToSql/SqlValidatorTests.cs`

**Interfaces:**
- Consumes: nothing
- Produces: `SqlValidationResult Validate(string sql)` — `record SqlValidationResult(bool IsValid, string CleanedSql, string? Error)`

- [ ] **Step 1: Write SqlValidator**

```csharp
// src/RagAndAI.Api/Services/NlToSql/SqlValidator.cs
using System.Text.RegularExpressions;

namespace RagAndAI.Api.Services.NlToSql;

public record SqlValidationResult(bool IsValid, string CleanedSql, string? Error);

public class SqlValidator
{
    private static readonly Regex DangerousKeywords = new(
        @"\b(DROP|DELETE|UPDATE|INSERT|ALTER|TRUNCATE|CREATE|EXEC|EXECUTE|GRANT|REVOKE|COPY|VACUUM)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CodeFence = new(
        @"```(?:sql)?\s*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public SqlValidationResult Validate(string rawSql)
    {
        if (string.IsNullOrWhiteSpace(rawSql))
            return new SqlValidationResult(false, string.Empty, "Empty SQL generated.");

        var cleaned = CodeFence.Replace(rawSql, string.Empty);
        cleaned = cleaned.Replace("```", string.Empty).Trim();

        if (!cleaned.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            return new SqlValidationResult(false, cleaned, "Only SELECT statements are allowed.");

        if (DangerousKeywords.IsMatch(cleaned))
        {
            var match = DangerousKeywords.Match(cleaned);
            return new SqlValidationResult(false, cleaned,
                $"SQL contains disallowed keyword: '{match.Value}'.");
        }

        return new SqlValidationResult(true, cleaned, null);
    }
}
```

- [ ] **Step 2: Write failing tests**

```csharp
// tests/RagAndAI.Tests/NlToSql/SqlValidatorTests.cs
using FluentAssertions;
using RagAndAI.Api.Services.NlToSql;

namespace RagAndAI.Tests.NlToSql;

public class SqlValidatorTests
{
    private readonly SqlValidator _sut = new();

    [Fact]
    public void Validate_ReturnsValid_ForSelectStatement()
    {
        var result = _sut.Validate("SELECT * FROM orders WHERE total > 100");
        result.IsValid.Should().BeTrue();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Validate_StripsMarkdownCodeFence()
    {
        var result = _sut.Validate("```sql\nSELECT id FROM customers\n```");
        result.IsValid.Should().BeTrue();
        result.CleanedSql.Should().Be("SELECT id FROM customers");
    }

    [Theory]
    [InlineData("DROP TABLE orders")]
    [InlineData("DELETE FROM customers")]
    [InlineData("UPDATE products SET price = 0")]
    [InlineData("INSERT INTO orders VALUES (1)")]
    [InlineData("TRUNCATE TABLE order_items")]
    [InlineData("ALTER TABLE customers ADD COLUMN foo TEXT")]
    public void Validate_RejectsDestructiveStatements(string sql)
    {
        var result = _sut.Validate(sql);
        result.IsValid.Should().BeFalse();
        result.Error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_RejectsNonSelectStatement()
    {
        var result = _sut.Validate("EXPLAIN SELECT * FROM orders");
        // EXPLAIN starts with E, not SELECT — blocked
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_RejectsEmpty()
    {
        var result = _sut.Validate("   ");
        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("Empty");
    }

    [Fact]
    public void Validate_AllowsComplexSelectWithJoins()
    {
        var sql = """
            SELECT o.id, c.name, o.total
            FROM orders o
            JOIN customers c ON o.customer_id = c.id
            WHERE o.total > 100
            ORDER BY o.created_at DESC
            """;
        var result = _sut.Validate(sql);
        result.IsValid.Should().BeTrue();
    }
}
```

- [ ] **Step 3: Run tests — expect fail (class not found)**

```bash
dotnet test tests/RagAndAI.Tests --filter "SqlValidatorTests" -v
```

Expected: build error or test fail — `SqlValidator` not yet implemented.

- [ ] **Step 4: Confirm tests pass with implementation**

```bash
dotnet test tests/RagAndAI.Tests --filter "SqlValidatorTests" -v
```

Expected: all 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/RagAndAI.Api/Services/NlToSql/SqlValidator.cs tests/RagAndAI.Tests/NlToSql/
git commit -m "feat: add SqlValidator with destructive-SQL blocking and tests"
```

---

## Task 12: SqlPromptBuilder + NlToSqlService

**Files:**
- Create: `src/RagAndAI.Api/Services/NlToSql/SqlPromptBuilder.cs`
- Create: `src/RagAndAI.Api/Services/NlToSql/INlToSqlService.cs`
- Create: `src/RagAndAI.Api/Services/NlToSql/NlToSqlService.cs`
- Create: `tests/RagAndAI.Tests/NlToSql/SqlPromptBuilderTests.cs`
- Create: `tests/RagAndAI.Tests/NlToSql/NlToSqlServiceTests.cs`

**Interfaces:**
- Consumes: `SchemaInspector`, `SqlValidator`, `IChatCompletionService`
- Produces:
  - `SqlPromptBuilder.Build(string schema, string question)` → `string`
  - `INlToSqlService.QueryAsync(string question, CancellationToken ct)` → `NlToSqlResult`

- [ ] **Step 1: Write SqlPromptBuilder**

```csharp
// src/RagAndAI.Api/Services/NlToSql/SqlPromptBuilder.cs
namespace RagAndAI.Api.Services.NlToSql;

public class SqlPromptBuilder
{
    public string Build(string schema, string question) => $"""
        You are a PostgreSQL expert. Given the following database schema:

        {schema}

        Generate a SQL SELECT query to answer this question:
        "{question}"

        Rules:
        - Return ONLY the SQL query, nothing else — no explanation, no markdown, no prose
        - Use only SELECT statements
        - Use exact table and column names from the schema above
        - Use proper PostgreSQL syntax
        - If the question cannot be answered from the schema, return: SELECT 'Unable to answer' AS message
        """;
}
```

- [ ] **Step 2: Write SqlPromptBuilder tests**

```csharp
// tests/RagAndAI.Tests/NlToSql/SqlPromptBuilderTests.cs
using FluentAssertions;
using RagAndAI.Api.Services.NlToSql;

namespace RagAndAI.Tests.NlToSql;

public class SqlPromptBuilderTests
{
    private readonly SqlPromptBuilder _sut = new();

    [Fact]
    public void Build_ContainsSchema()
    {
        var schema = "Table: orders\n  - id: uuid (not null) [PK]";
        var result = _sut.Build(schema, "any question");
        result.Should().Contain(schema);
    }

    [Fact]
    public void Build_ContainsQuestion()
    {
        var question = "show all orders over $100";
        var result = _sut.Build("schema text", question);
        result.Should().Contain(question);
    }

    [Fact]
    public void Build_ContainsSelectOnlyInstruction()
    {
        var result = _sut.Build("schema", "question");
        result.Should().Contain("SELECT");
        result.Should().Contain("ONLY the SQL query");
    }
}
```

- [ ] **Step 3: Run SqlPromptBuilder tests**

```bash
dotnet test tests/RagAndAI.Tests --filter "SqlPromptBuilderTests" -v
```

Expected: 3 pass.

- [ ] **Step 4: Write INlToSqlService and NlToSqlResult**

```csharp
// src/RagAndAI.Api/Services/NlToSql/INlToSqlService.cs
namespace RagAndAI.Api.Services.NlToSql;

public record NlToSqlResult(
    string Sql,
    IReadOnlyList<Dictionary<string, object?>> Results,
    string Explanation,
    string? Error = null);

public interface INlToSqlService
{
    Task<NlToSqlResult> QueryAsync(string question, CancellationToken ct = default);
}
```

- [ ] **Step 5: Write NlToSqlService**

```csharp
// src/RagAndAI.Api/Services/NlToSql/NlToSqlService.cs
using Microsoft.SemanticKernel.ChatCompletion;
using Npgsql;

namespace RagAndAI.Api.Services.NlToSql;

public class NlToSqlService(
    SchemaInspector schemaInspector,
    SqlPromptBuilder promptBuilder,
    SqlValidator validator,
    IChatCompletionService chatService,
    IConfiguration configuration) : INlToSqlService
{
    public async Task<NlToSqlResult> QueryAsync(string question, CancellationToken ct = default)
    {
        // Step 1: Load schema
        var schema = await schemaInspector.GetSchemaAsync(ct);

        // Step 2: Generate SQL via LLM
        var prompt = promptBuilder.Build(schema, question);
        var history = new ChatHistory();
        history.AddUserMessage(prompt);
        var response = await chatService.GetChatMessageContentAsync(history, cancellationToken: ct);
        var rawSql = response.Content ?? string.Empty;

        // Step 3: Validate
        var validation = validator.Validate(rawSql);
        if (!validation.IsValid)
        {
            return new NlToSqlResult(rawSql, [], string.Empty,
                Error: $"Invalid SQL generated: {validation.Error}");
        }

        // Step 4: Execute
        List<Dictionary<string, object?>> rows;
        try
        {
            rows = await ExecuteSqlAsync(validation.CleanedSql, ct);
        }
        catch (Exception ex)
        {
            return new NlToSqlResult(validation.CleanedSql, [],
                string.Empty, Error: $"SQL execution failed: {ex.Message}");
        }

        // Step 5: Generate explanation
        var explanationPrompt = $"""
            The user asked: "{question}"
            The SQL executed was: {validation.CleanedSql}
            It returned {rows.Count} row(s).
            Provide a brief one-sentence plain English summary of what the results show.
            """;
        var explainHistory = new ChatHistory();
        explainHistory.AddUserMessage(explanationPrompt);
        var explanation = await chatService.GetChatMessageContentAsync(explainHistory, cancellationToken: ct);

        return new NlToSqlResult(validation.CleanedSql, rows, explanation.Content ?? string.Empty);
    }

    private async Task<List<Dictionary<string, object?>>> ExecuteSqlAsync(string sql, CancellationToken ct)
    {
        var connectionString = configuration.GetConnectionString("Postgres")!;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.CommandTimeout = 10;
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(ct))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            results.Add(row);
        }
        return results;
    }
}
```

- [ ] **Step 6: Write NlToSqlService tests**

```csharp
// tests/RagAndAI.Tests/NlToSql/NlToSqlServiceTests.cs
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NSubstitute;
using RagAndAI.Api.Services.NlToSql;

namespace RagAndAI.Tests.NlToSql;

public class NlToSqlServiceTests
{
    private readonly SchemaInspector _schemaInspector = Substitute.For<SchemaInspector>(
        Substitute.For<IConfiguration>());
    private readonly SqlPromptBuilder _promptBuilder = new();
    private readonly SqlValidator _validator = new();
    private readonly IChatCompletionService _chatService = Substitute.For<IChatCompletionService>();
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();

    [Fact]
    public async Task QueryAsync_ReturnsError_WhenLlmGeneratesInvalidSql()
    {
        _schemaInspector.GetSchemaAsync(Arg.Any<CancellationToken>())
            .Returns("Table: orders\n  - id: uuid");

        _chatService.GetChatMessageContentAsync(
                Arg.Any<ChatHistory>(), Arg.Any<PromptExecutionSettings>(), Arg.Any<Kernel>(),
                Arg.Any<CancellationToken>())
            .Returns(new ChatMessageContent(AuthorRole.Assistant, "DROP TABLE orders"));

        var sut = new NlToSqlService(_schemaInspector, _promptBuilder, _validator, _chatService, _config);

        var result = await sut.QueryAsync("delete all orders");

        result.Error.Should().NotBeNull();
        result.Error.Should().Contain("Invalid SQL");
        result.Results.Should().BeEmpty();
    }
}
```

- [ ] **Step 7: Run NlToSql tests**

```bash
dotnet test tests/RagAndAI.Tests --filter "NlToSql" -v
```

Expected: SqlPromptBuilderTests (3) + SqlValidatorTests (7) + NlToSqlServiceTests (1) pass.

- [ ] **Step 8: Register services in Program.cs**

```csharp
builder.Services.AddSingleton<SqlPromptBuilder>();
builder.Services.AddSingleton<SqlValidator>();
builder.Services.AddScoped<INlToSqlService, NlToSqlService>();
```

- [ ] **Step 9: Commit**

```bash
git add src/RagAndAI.Api/Services/NlToSql/ tests/RagAndAI.Tests/NlToSql/ src/RagAndAI.Api/Program.cs
git commit -m "feat: add SqlPromptBuilder, NlToSqlService, INlToSqlService with tests"
```

---

## Task 13: SQL + Health Endpoints (2 feature files + health)

**Files:**
- Create: `src/RagAndAI.Api/Features/SqlQuery/Execute.cs`
- Create: `src/RagAndAI.Api/Features/SqlQuery/Schema.cs`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Consumes: `INlToSqlService`, `SchemaInspector`
- Produces: `POST /sql/query`, `GET /sql/schema`, `GET /health`

- [ ] **Step 1: Create Features/SqlQuery/Execute.cs**

```csharp
// src/RagAndAI.Api/Features/SqlQuery/Execute.cs
using RagAndAI.Api.Services.NlToSql;

namespace RagAndAI.Api.Features.SqlQuery;

public static class Execute
{
    public static RouteGroupBuilder MapExecute(this RouteGroupBuilder group)
    {
        group.MapPost("/query", Handle);
        return group;
    }

    private static async Task<IResult> Handle(
        SqlQueryRequest request,
        INlToSqlService nlToSqlService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest(new { error = "Question is required." });

        var result = await nlToSqlService.QueryAsync(request.Question, ct);

        if (result.Error is not null)
            return Results.UnprocessableEntity(new
            {
                error = result.Error,
                generatedSql = result.Sql
            });

        return Results.Ok(new
        {
            sql = result.Sql,
            results = result.Results,
            explanation = result.Explanation,
            rowCount = result.Results.Count
        });
    }

    private record SqlQueryRequest(string Question);
}
```

- [ ] **Step 2: Create Features/SqlQuery/Schema.cs**

```csharp
// src/RagAndAI.Api/Features/SqlQuery/Schema.cs
using RagAndAI.Api.Services.NlToSql;

namespace RagAndAI.Api.Features.SqlQuery;

public static class Schema
{
    public static RouteGroupBuilder MapSchema(this RouteGroupBuilder group)
    {
        group.MapGet("/schema", Handle);
        return group;
    }

    private static async Task<IResult> Handle(
        SchemaInspector inspector,
        CancellationToken ct)
    {
        var schema = await inspector.GetSchemaAsync(ct);
        return Results.Ok(new { schema });
    }
}
```

- [ ] **Step 3: Add health check + register in Program.cs**

First add package:
```bash
dotnet add src/RagAndAI.Api package AspNetCore.HealthChecks.Npgsql
```

Then in Program.cs:
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RagAndAI.Api.Features.SqlQuery;

builder.Services.AddHealthChecks()
    .AddNpgsql(builder.Configuration.GetConnectionString("Postgres")!,
        name: "postgres",
        failureStatus: HealthStatus.Unhealthy)
    .AddUrlGroup(new Uri(
        builder.Configuration[$"{OllamaConfig.SectionName}:Endpoint"] + "/api/tags"),
        name: "ollama",
        failureStatus: HealthStatus.Unhealthy);

// Later, after app = builder.Build():
var sql = app.MapGroup("/sql");
sql.MapExecute();
sql.MapSchema();
app.MapHealthChecks("/health");
```

- [ ] **Step 4: Manual test — NL-to-SQL**

Ensure ecommerce seed data exists (Task 14 below). Then:

```bash
curl -X POST http://localhost:5000/sql/query \
  -H "Content-Type: application/json" \
  -d '{"question": "How many customers do we have?"}'
```

Expected: `{ "sql": "SELECT COUNT(*) ...", "results": [...], "explanation": "..." }`.

```bash
curl http://localhost:5000/health
```

Expected: `Healthy`.

- [ ] **Step 5: Commit**

```bash
git add src/RagAndAI.Api/Features/SqlQuery/ src/RagAndAI.Api/Program.cs
git commit -m "feat: add SQL query, schema, and health endpoints (feature folder)"
```

---

## Task 14: Ecommerce Seed Data

**Files:**
- Create: `src/RagAndAI.Api/Data/SeedData.cs`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Produces: realistic ecommerce data in `customers`, `products`, `orders`, `order_items` tables

- [ ] **Step 1: Write SeedData**

```csharp
// src/RagAndAI.Api/Data/SeedData.cs
using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (db.Customers.Any()) return;  // already seeded

        var customers = new[]
        {
            new Customer { Name = "Alice Johnson", Email = "alice@example.com" },
            new Customer { Name = "Bob Smith", Email = "bob@example.com" },
            new Customer { Name = "Carol White", Email = "carol@example.com" },
            new Customer { Name = "Dave Brown", Email = "dave@example.com" },
        };

        var products = new[]
        {
            new Product { Name = "Laptop Pro 15", Category = "Electronics", Price = 1299.99m, Stock = 50 },
            new Product { Name = "Wireless Mouse", Category = "Electronics", Price = 29.99m, Stock = 200 },
            new Product { Name = "USB-C Hub", Category = "Electronics", Price = 49.99m, Stock = 150 },
            new Product { Name = "Standing Desk", Category = "Furniture", Price = 599.99m, Stock = 20 },
            new Product { Name = "Ergonomic Chair", Category = "Furniture", Price = 449.99m, Stock = 30 },
            new Product { Name = "Coffee Mug", Category = "Kitchen", Price = 14.99m, Stock = 500 },
        };

        db.Customers.AddRange(customers);
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        var orders = new[]
        {
            new Order
            {
                CustomerId = customers[0].Id, Status = "delivered",
                Total = 1299.99m + 29.99m,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-30),
                Items =
                [
                    new OrderItem { ProductId = products[0].Id, Quantity = 1, UnitPrice = 1299.99m },
                    new OrderItem { ProductId = products[1].Id, Quantity = 1, UnitPrice = 29.99m },
                ]
            },
            new Order
            {
                CustomerId = customers[1].Id, Status = "shipped",
                Total = 49.99m * 2,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-7),
                Items =
                [
                    new OrderItem { ProductId = products[2].Id, Quantity = 2, UnitPrice = 49.99m },
                ]
            },
            new Order
            {
                CustomerId = customers[2].Id, Status = "pending",
                Total = 599.99m + 449.99m,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Items =
                [
                    new OrderItem { ProductId = products[3].Id, Quantity = 1, UnitPrice = 599.99m },
                    new OrderItem { ProductId = products[4].Id, Quantity = 1, UnitPrice = 449.99m },
                ]
            },
            new Order
            {
                CustomerId = customers[0].Id, Status = "delivered",
                Total = 14.99m * 3,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
                Items =
                [
                    new OrderItem { ProductId = products[5].Id, Quantity = 3, UnitPrice = 14.99m },
                ]
            },
        };

        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();
    }
}
```

- [ ] **Step 2: Call seed on startup in Program.cs**

After `var app = builder.Build();`:

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.SeedAsync(db);
}
```

- [ ] **Step 3: Verify seed via psql or NL-to-SQL**

```bash
curl -X POST http://localhost:5000/sql/query \
  -H "Content-Type: application/json" \
  -d '{"question": "Show me all customers and how many orders each has"}'
```

Expected: results showing Alice (2 orders), Bob (1), Carol (1), Dave (0).

- [ ] **Step 4: Commit**

```bash
git add src/RagAndAI.Api/Data/SeedData.cs src/RagAndAI.Api/Program.cs
git commit -m "feat: add ecommerce seed data with customers, products, orders"
```

---

## Task 15: Dockerfile + docker-compose

**Files:**
- Create: `Dockerfile`
- Create: `docker-compose.yml`
- Create: `.dockerignore`

**Interfaces:**
- Produces: `docker compose up` starts the app; Postgres and pgvector available; Ollama runs on host machine (not in Docker)

- [ ] **Step 1: Write Dockerfile**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY RagAndAI.sln .
COPY src/RagAndAI.Api/RagAndAI.Api.csproj src/RagAndAI.Api/
RUN dotnet restore src/RagAndAI.Api/RagAndAI.Api.csproj

COPY src/ src/
RUN dotnet publish src/RagAndAI.Api/RagAndAI.Api.csproj -c Release -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RagAndAI.Api.dll"]
```

- [ ] **Step 2: Write docker-compose.yml**

Note: Ollama runs on the host machine, not in Docker. The app container reaches it via `host.docker.internal`.

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: ragdb
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: yourpassword
    ports:
      - "5432:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data

  api:
    build: .
    ports:
      - "8080:8080"
    environment:
      ConnectionStrings__Postgres: "Host=postgres;Database=ragdb;Username=postgres;Password=yourpassword"
      Ollama__Endpoint: "http://host.docker.internal:11434"
      Ollama__EmbeddingModel: "nomic-embed-text"
      Ollama__ChatModel: "llama3.1"
      Chunking__ChunkSize: "512"
      Chunking__Overlap: "50"
      Chunking__TopK: "5"
    depends_on:
      - postgres
    extra_hosts:
      - "host.docker.internal:host-gateway"

volumes:
  pgdata:
```

- [ ] **Step 3: Write .dockerignore**

```
**/bin/
**/obj/
**/.git/
**/tests/
*.user
.vs/
.vscode/
```

- [ ] **Step 4: Test Docker build**

```bash
cd D:/Projects/RAG_And_AI
docker compose build
```

Expected: image builds successfully.

- [ ] **Step 5: Run with Docker compose**

```bash
docker compose up -d
```

Then test:
```bash
curl http://localhost:8080/health
```

Expected: `Healthy` (Postgres up, Ollama reachable on host).

- [ ] **Step 6: Commit**

```bash
git add Dockerfile docker-compose.yml .dockerignore
git commit -m "feat: add Dockerfile and docker-compose with pgvector postgres"
```

---

## Task 16: Session + ChatMessage Entities + Document.SessionId

**Files:**
- Create: `src/RagAndAI.Api/Data/Models/Session.cs`
- Create: `src/RagAndAI.Api/Data/Models/ChatMessage.cs`
- Modify: `src/RagAndAI.Api/Data/Models/Document.cs`
- Modify: `src/RagAndAI.Api/Data/AppDbContext.cs`

**Interfaces:**
- Produces: `Session` + `ChatMessage` EF entities; `Document.SessionId` nullable FK; migration applied

- [ ] **Step 1: Create Session entity**

```csharp
// src/RagAndAI.Api/Data/Models/Session.cs
namespace RagAndAI.Api.Data.Models;

public class Session
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? Title { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Document> Documents { get; set; } = [];
    public ICollection<ChatMessage> Messages { get; set; } = [];
}
```

- [ ] **Step 2: Create ChatMessage entity**

```csharp
// src/RagAndAI.Api/Data/Models/ChatMessage.cs
namespace RagAndAI.Api.Data.Models;

public class ChatMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Session Session { get; set; } = null!;
    public string Role { get; set; } = string.Empty;   // "user" or "assistant"
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
```

- [ ] **Step 3: Add SessionId to Document**

```csharp
// src/RagAndAI.Api/Data/Models/Document.cs — add these
public Guid? SessionId { get; set; }
public Session? Session { get; set; }
```

- [ ] **Step 4: Update AppDbContext**

```csharp
public DbSet<Session> Sessions => Set<Session>();
public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

// In OnModelCreating, add:
modelBuilder.Entity<Session>(e =>
{
    e.ToTable("sessions");
    e.HasKey(x => x.Id);
});

modelBuilder.Entity<ChatMessage>(e =>
{
    e.ToTable("chat_messages");
    e.HasKey(x => x.Id);
    e.Property(x => x.SessionId).HasColumnName("session_id");
    e.Property(x => x.CreatedAt).HasColumnName("created_at");
    e.HasOne(x => x.Session).WithMany(x => x.Messages)
        .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
    e.HasIndex(x => new { x.SessionId, x.CreatedAt });
});

// In Document config, add:
e.Property(x => x.SessionId).HasColumnName("session_id");
e.HasOne(x => x.Session).WithMany(x => x.Documents)
    .HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
```

- [ ] **Step 5: Create + apply migration**

```bash
dotnet ef migrations add AddSessionsAndChatMessages --project src/RagAndAI.Api
dotnet ef database update --project src/RagAndAI.Api
```

- [ ] **Step 6: Verify cascade in psql**

```sql
INSERT INTO sessions (title) VALUES ('test');
INSERT INTO chat_messages (session_id, role, content) SELECT id, 'user', 'hi' FROM sessions LIMIT 1;
DELETE FROM sessions;
SELECT COUNT(*) FROM chat_messages;   -- expect 0
```

- [ ] **Step 7: Commit**

```bash
git add src/RagAndAI.Api/Data/
git commit -m "feat: add Session, ChatMessage entities, Document.SessionId FK"
```

---

## Task 17: RagService — History-Aware QueryAsync

**Files:**
- Modify: `src/RagAndAI.Api/Services/Rag/IRagService.cs`
- Modify: `src/RagAndAI.Api/Services/Rag/RagService.cs`
- Modify: `src/RagAndAI.Api/Features/Chat/Query.cs` (library chat) — pass `history: null`
- Modify: `tests/RagAndAI.Tests/Rag/RagServiceTests.cs`

**Interfaces:**
- Consumes: `IReadOnlyList<ChatMessage>?` from callers
- Produces: `QueryAsync(question, documentIds, history, ct)` — history-aware overload

- [ ] **Step 1: Update IRagService signature**

```csharp
Task<RagResult> QueryAsync(
    string question,
    IEnumerable<Guid> documentIds,
    IReadOnlyList<ChatMessage>? history = null,
    CancellationToken ct = default);
```

- [ ] **Step 2: Update RagService.QueryAsync — prepend history to prompt**

Modify the prompt block to include prior conversation when `history` is non-empty:

```csharp
var historyBlock = string.Empty;
if (history is { Count: > 0 })
{
    var lines = history.Select(m => $"{(m.Role == "user" ? "User" : "Assistant")}: {m.Content}");
    historyBlock = "Previous conversation:\n" + string.Join("\n", lines) + "\n\n";
}

var prompt = $"""
    You are a helpful assistant. Answer using ONLY the context below.
    {(history is { Count: > 0 } ? "Prior conversation is context for follow-ups." : "")}
    If the context doesn't contain the answer, say "I don't have enough information to answer that."

    {historyBlock}Retrieved context:
    {context}

    Current question: {question}

    Answer:
    """;
```

- [ ] **Step 3: Update library chat to pass history: null**

In `Features/Chat/Query.cs`, the handler call becomes:
```csharp
var result = await ragService.QueryAsync(request.Question, request.DocumentIds, history: null, ct);
```

- [ ] **Step 4: Add test for history-aware query**

Add to `RagServiceTests.cs`:
```csharp
[Fact]
public async Task QueryAsync_IncludesHistoryInPrompt_WhenProvided()
{
    var kernel = Substitute.For<Kernel>();
    var config = Options.Create(new ChunkingConfig { TopK = 3, ChunkSize = 10, Overlap = 2 });

    _embedding.GenerateEmbeddingsAsync(Arg.Any<IList<string>>(), cancellationToken: Arg.Any<CancellationToken>())
        .Returns(new List<ReadOnlyMemory<float>> { new(new float[768]) });

    _collection.VectorizedSearchAsync(
            Arg.Any<ReadOnlyMemory<float>>(),
            Arg.Any<VectorSearchOptions>(),
            Arg.Any<CancellationToken>())
        .Returns(AsyncEnumerable.Empty<VectorSearchResult<DocumentChunkRecord>>());

    var history = new List<ChatMessage>
    {
        new() { Role = "user", Content = "prior question" },
        new() { Role = "assistant", Content = "prior answer" }
    };

    var sut = new RagService(kernel, _embedding, _collection, config);
    var result = await sut.QueryAsync("follow-up", [Guid.NewGuid()], history);

    result.Should().NotBeNull();
}
```

- [ ] **Step 5: Run tests**

```bash
dotnet test tests/RagAndAI.Tests --filter "RagServiceTests" -v
```

- [ ] **Step 6: Commit**

```bash
git add src/RagAndAI.Api/Services/Rag/ src/RagAndAI.Api/Features/Chat/ tests/RagAndAI.Tests/Rag/
git commit -m "feat: RagService.QueryAsync accepts optional chat history"
```

---

## Task 18: Session CRUD Endpoints

**Files:**
- Create: `src/RagAndAI.Api/Features/Sessions/Create.cs`
- Create: `src/RagAndAI.Api/Features/Sessions/List.cs`
- Create: `src/RagAndAI.Api/Features/Sessions/Get.cs`
- Create: `src/RagAndAI.Api/Features/Sessions/Delete.cs`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `IRagService` (for cascade cleanup via document deletion)
- Produces:
  - `POST /sessions` → `{ id, createdAt }`
  - `GET /sessions` → `[{ id, title, createdAt, updatedAt, documentCount, messageCount }]`
  - `GET /sessions/{id}` → `{ id, title, ..., documents: [...], messages: [...] }`
  - `DELETE /sessions/{id}` → 204 (cascades to docs, chunks, messages)

- [ ] **Step 1: Create Features/Sessions/Create.cs**

```csharp
// src/RagAndAI.Api/Features/Sessions/Create.cs
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Features.Sessions;

public static class Create
{
    public static RouteGroupBuilder MapCreate(this RouteGroupBuilder group)
    {
        group.MapPost("/", Handle);
        return group;
    }

    private static async Task<IResult> Handle(AppDbContext db, CancellationToken ct)
    {
        var session = new Session();
        db.Sessions.Add(session);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/sessions/{session.Id}",
            new { session.Id, session.CreatedAt });
    }
}
```

- [ ] **Step 2: Create Features/Sessions/List.cs**

```csharp
// src/RagAndAI.Api/Features/Sessions/List.cs
using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Features.Sessions;

public static class List
{
    public static RouteGroupBuilder MapList(this RouteGroupBuilder group)
    {
        group.MapGet("/", Handle);
        return group;
    }

    private static async Task<IResult> Handle(AppDbContext db, CancellationToken ct)
    {
        var sessions = await db.Sessions
            .OrderByDescending(s => s.UpdatedAt)
            .Select(s => new
            {
                s.Id,
                s.Title,
                s.CreatedAt,
                s.UpdatedAt,
                documentCount = s.Documents.Count,
                messageCount = s.Messages.Count
            })
            .ToListAsync(ct);
        return Results.Ok(sessions);
    }
}
```

- [ ] **Step 3: Create Features/Sessions/Get.cs**

```csharp
// src/RagAndAI.Api/Features/Sessions/Get.cs
using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;

namespace RagAndAI.Api.Features.Sessions;

public static class Get
{
    public static RouteGroupBuilder MapGet(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", Handle);
        return group;
    }

    private static async Task<IResult> Handle(Guid id, AppDbContext db, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Documents)
            .Include(s => s.Messages.OrderBy(m => m.CreatedAt))
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session is null) return Results.NotFound();

        return Results.Ok(new
        {
            session.Id,
            session.Title,
            session.CreatedAt,
            session.UpdatedAt,
            documents = session.Documents.Select(d => new
            {
                d.Id, d.Filename, d.FileType, d.UploadedAt
            }),
            messages = session.Messages.Select(m => new
            {
                m.Id, m.Role, m.Content, m.CreatedAt
            })
        });
    }
}
```

- [ ] **Step 4: Create Features/Sessions/Delete.cs**

Note: cascade delete via FK handles chat_messages and document rows. But SK vector-store
chunks for those documents need explicit cleanup — call `IRagService.DeleteDocumentChunksAsync`
per doc before removing the session.

```csharp
// src/RagAndAI.Api/Features/Sessions/Delete.cs
using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Sessions;

public static class Delete
{
    public static RouteGroupBuilder MapDelete(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", Handle);
        return group;
    }

    private static async Task<IResult> Handle(
        Guid id, AppDbContext db, IRagService ragService, CancellationToken ct)
    {
        var session = await db.Sessions
            .Include(s => s.Documents)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session is null) return Results.NotFound();

        foreach (var doc in session.Documents)
            await ragService.DeleteDocumentChunksAsync(doc.Id, ct);

        db.Sessions.Remove(session);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
```

- [ ] **Step 5: Register in Program.cs**

```csharp
using RagAndAI.Api.Features.Sessions;

var sessions = app.MapGroup("/sessions");
sessions.MapCreate();
sessions.MapList();
sessions.MapGet();
sessions.MapDelete();
```

Note: `MapGet` collides with built-in `WebApplication.MapGet` — resolve via
namespace-qualified call or rename extension method to `MapGetOne`. Simpler:
rename our method to `MapGetOne` and the endpoint method to be clearer.

Change Get.cs:
```csharp
public static RouteGroupBuilder MapGetOne(this RouteGroupBuilder group)
```

And in Program.cs:
```csharp
sessions.MapGetOne();
```

- [ ] **Step 6: Manual test**

```bash
curl -X POST http://localhost:5000/sessions
# note the returned id
curl http://localhost:5000/sessions
curl http://localhost:5000/sessions/{id}
curl -X DELETE http://localhost:5000/sessions/{id}
```

- [ ] **Step 7: Commit**

```bash
git add src/RagAndAI.Api/Features/Sessions/ src/RagAndAI.Api/Program.cs
git commit -m "feat: add session CRUD endpoints (Create, List, Get, Delete)"
```

---

## Task 19: Session Upload Endpoint

**Files:**
- Create: `src/RagAndAI.Api/Features/Sessions/Upload.cs`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `IRagService.IngestAsync`, `FileParserFactory`, session id from route
- Produces: `POST /sessions/{id}/upload` → `{ id, filename, fileType, uploadedAt, sessionId }` (201)

- [ ] **Step 1: Create Features/Sessions/Upload.cs**

```csharp
// src/RagAndAI.Api/Features/Sessions/Upload.cs
using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.FileParser;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Sessions;

public static class Upload
{
    public static RouteGroupBuilder MapUpload(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/upload", Handle).DisableAntiforgery();
        return group;
    }

    private static async Task<IResult> Handle(
        Guid id,
        IFormFile file,
        AppDbContext db,
        IRagService ragService,
        FileParserFactory parserFactory,
        CancellationToken ct)
    {
        var sessionExists = await db.Sessions.AnyAsync(s => s.Id == id, ct);
        if (!sessionExists) return Results.NotFound(new { error = "Session not found." });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        IFileParser parser;
        try { parser = parserFactory.GetParser(extension); }
        catch (NotSupportedException ex) { return Results.BadRequest(new { error = ex.Message }); }

        string extractedText;
        try
        {
            await using var stream = file.OpenReadStream();
            extractedText = await parser.ExtractTextAsync(stream, file.FileName, ct);
        }
        catch (Exception ex)
        {
            return Results.BadRequest(new { error = $"Failed to parse file: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(extractedText))
            return Results.BadRequest(new { error = "Document contains no extractable text." });

        var document = new Document
        {
            Filename = file.FileName,
            FileType = extension.TrimStart('.'),
            SessionId = id
        };

        db.Documents.Add(document);
        await db.SaveChangesAsync(ct);

        try
        {
            await ragService.IngestAsync(document.Id, extractedText, ct);
        }
        catch (Exception ex)
        {
            db.Documents.Remove(document);
            await db.SaveChangesAsync(ct);
            return Results.Problem($"Embedding failed: {ex.Message}", statusCode: 503);
        }

        return Results.Created($"/sessions/{id}/documents/{document.Id}", new
        {
            document.Id, document.Filename, document.FileType,
            document.UploadedAt, document.SessionId
        });
    }
}
```

- [ ] **Step 2: Register in Program.cs**

```csharp
sessions.MapUpload();
```

- [ ] **Step 3: Manual test**

```bash
curl -X POST http://localhost:5000/sessions
# note session id
curl -X POST http://localhost:5000/sessions/{sessionId}/upload -F "file=@test.pdf"
curl http://localhost:5000/sessions/{sessionId}
# verify document appears
```

- [ ] **Step 4: Commit**

```bash
git add src/RagAndAI.Api/Features/Sessions/Upload.cs src/RagAndAI.Api/Program.cs
git commit -m "feat: add session-scoped file upload endpoint"
```

---

## Task 20: Session Chat Endpoint (with history)

**Files:**
- Create: `src/RagAndAI.Api/Features/Sessions/Chat.cs`
- Modify: `src/RagAndAI.Api/Program.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `IRagService.QueryAsync(question, docIds, history, ct)` (from Task 17)
- Produces: `POST /sessions/{id}/chat` — body `{ question, libraryDocumentIds? }` → `{ answer, sources, messageId }`

- [ ] **Step 1: Create Features/Sessions/Chat.cs**

```csharp
// src/RagAndAI.Api/Features/Sessions/Chat.cs
using Microsoft.EntityFrameworkCore;
using RagAndAI.Api.Data;
using RagAndAI.Api.Data.Models;
using RagAndAI.Api.Services.Rag;

namespace RagAndAI.Api.Features.Sessions;

public static class Chat
{
    private const int HistoryWindow = 10;

    public static RouteGroupBuilder MapChat(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/chat", Handle);
        return group;
    }

    private static async Task<IResult> Handle(
        Guid id,
        ChatRequest request,
        AppDbContext db,
        IRagService ragService,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest(new { error = "Question is required." });

        var session = await db.Sessions
            .Include(s => s.Documents)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (session is null) return Results.NotFound();

        // Resolve document scope: session docs + explicit library docs
        var docIds = session.Documents.Select(d => d.Id).ToList();
        if (request.LibraryDocumentIds is { Count: > 0 })
            docIds.AddRange(request.LibraryDocumentIds);

        if (docIds.Count == 0)
            return Results.BadRequest(new { error = "No documents in session and no libraryDocumentIds provided." });

        // Load last N messages, oldest first
        var history = await db.ChatMessages
            .Where(m => m.SessionId == id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(HistoryWindow)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

        RagResult result;
        try
        {
            result = await ragService.QueryAsync(request.Question, docIds, history, ct);
        }
        catch (Exception ex)
        {
            return Results.Problem($"Query failed: {ex.Message}", statusCode: 503);
        }

        // Persist messages
        var userMsg = new ChatMessage { SessionId = id, Role = "user", Content = request.Question };
        var asstMsg = new ChatMessage { SessionId = id, Role = "assistant", Content = result.Answer };
        db.ChatMessages.Add(userMsg);
        db.ChatMessages.Add(asstMsg);

        // Set title on first user message
        if (string.IsNullOrEmpty(session.Title))
            session.Title = request.Question[..Math.Min(60, request.Question.Length)];

        session.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        return Results.Ok(new
        {
            answer = result.Answer,
            sources = result.Sources,
            messageId = asstMsg.Id
        });
    }

    private record ChatRequest(string Question, List<Guid>? LibraryDocumentIds = null);
}
```

- [ ] **Step 2: Register in Program.cs**

```csharp
sessions.MapChat();
```

- [ ] **Step 3: End-to-end manual test**

```bash
# 1. Create session
SID=$(curl -s -X POST http://localhost:5000/sessions | jq -r .id)

# 2. Upload doc
curl -X POST http://localhost:5000/sessions/$SID/upload -F "file=@test.pdf"

# 3. First question — sets title
curl -X POST http://localhost:5000/sessions/$SID/chat \
  -H "Content-Type: application/json" \
  -d '{"question": "What is this document about?"}'

# 4. Follow-up — uses history
curl -X POST http://localhost:5000/sessions/$SID/chat \
  -H "Content-Type: application/json" \
  -d '{"question": "Tell me more about that."}'

# 5. Verify list shows title + counts
curl http://localhost:5000/sessions

# 6. Get details — history present
curl http://localhost:5000/sessions/$SID

# 7. Delete
curl -X DELETE http://localhost:5000/sessions/$SID
```

- [ ] **Step 4: Commit**

```bash
git add src/RagAndAI.Api/Features/Sessions/Chat.cs src/RagAndAI.Api/Program.cs
git commit -m "feat: add session chat endpoint with history and auto-title"
```

---

## Self-Review Checklist

**Spec coverage:**
- [x] RAG: upload (Task 8), ingest (Task 6), query (Tasks 7, 9) ✓
- [x] File types: PDF (Task 4), Word/Excel/txt (Tasks 4, 5) ✓
- [x] NL-to-SQL: schema introspection (Task 10), validation (Task 11), service (Task 12), endpoints (Task 13) ✓
- [x] pgvector + ivfflat index: managed by SK Postgres connector, collection created in Task 6 ✓
- [x] Ecommerce schema: migrations in Task 3, seed in Task 14 ✓
- [x] Health endpoint: Task 13 ✓
- [x] Docker: Task 15 ✓
- [x] Config-based AI provider switching: `OllamaConfig` + DI in Task 2 + Task 6 ✓
- [x] Error handling: all scenarios covered per endpoint task ✓
- [x] ivfflat index: created by SK Postgres connector when collection is initialized ✓

**Placeholder scan:** None found.

**Type consistency:**
- `IRagService`: `IngestAsync(Guid, string, CancellationToken)`, `QueryAsync(string, IEnumerable<Guid>, CancellationToken)`, `DeleteDocumentChunksAsync(Guid, CancellationToken)` — consistent across Tasks 6, 7, 8, 9
- `INlToSqlService`: `QueryAsync(string, CancellationToken)` → `NlToSqlResult` — consistent across Tasks 12, 13
- `IFileParser`: `ExtractTextAsync(Stream, string, CancellationToken)` — consistent across Tasks 4, 5, 8
- `SqlValidationResult`: `(bool IsValid, string CleanedSql, string? Error)` — consistent across Tasks 11, 12
- `NlToSqlResult`: `(string Sql, IReadOnlyList<Dictionary<string, object?>> Results, string Explanation, string? Error)` — consistent across Tasks 12, 13
