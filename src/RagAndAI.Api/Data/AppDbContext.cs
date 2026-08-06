using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentChunkRecord> DocumentChunks => Set<DocumentChunkRecord>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<DocumentChunkRecord>(e =>
        {
            e.ToTable("document_chunks");
            e.HasKey(x => x.Id);
            e.Property(x => x.DocumentId).HasColumnName("document_id");
            e.Property(x => x.ChunkIndex).HasColumnName("chunk_index");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.Embedding).HasColumnName("embedding").HasColumnType("vector(768)");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasIndex(x => x.DocumentId);
        });

        modelBuilder.Entity<Document>(e =>
        {
            e.ToTable("documents");
            e.HasKey(x => x.Id);
            e.Property(x => x.Filename).HasColumnName("filename").IsRequired();
            e.Property(x => x.FileType).HasColumnName("file_type").IsRequired();
            e.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            e.Property(x => x.Metadata).HasColumnName("metadata");
            e.Property(x => x.SessionId).HasColumnName("session_id");
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
