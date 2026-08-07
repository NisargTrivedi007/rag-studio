using RagAndAI.Api.Data.Models;

namespace RagAndAI.Api.Data;

public static class SeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        // Only seed if tables are empty
        if (db.Customers.Any() || db.Products.Any() || db.Orders.Any())
            return;

        // Customers
        var customers = new[]
        {
            new Customer { Id = Guid.NewGuid(), Name = "Alice Johnson", Email = "alice@example.com", CreatedAt = DateTimeOffset.UtcNow },
            new Customer { Id = Guid.NewGuid(), Name = "Bob Smith", Email = "bob@example.com", CreatedAt = DateTimeOffset.UtcNow },
            new Customer { Id = Guid.NewGuid(), Name = "Carol Davis", Email = "carol@example.com", CreatedAt = DateTimeOffset.UtcNow },
            new Customer { Id = Guid.NewGuid(), Name = "David Brown", Email = "david@example.com", CreatedAt = DateTimeOffset.UtcNow },
        };
        db.Customers.AddRange(customers);
        await db.SaveChangesAsync();

        // Products
        var products = new[]
        {
            new Product { Id = Guid.NewGuid(), Name = "Laptop", Category = "Electronics", Price = 999.99m, Stock = 5 },
            new Product { Id = Guid.NewGuid(), Name = "Mouse", Category = "Electronics", Price = 29.99m, Stock = 50 },
            new Product { Id = Guid.NewGuid(), Name = "Keyboard", Category = "Electronics", Price = 79.99m, Stock = 30 },
            new Product { Id = Guid.NewGuid(), Name = "Monitor", Category = "Electronics", Price = 299.99m, Stock = 10 },
            new Product { Id = Guid.NewGuid(), Name = "Desk Chair", Category = "Furniture", Price = 199.99m, Stock = 15 },
            new Product { Id = Guid.NewGuid(), Name = "Desk Lamp", Category = "Lighting", Price = 49.99m, Stock = 25 },
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        // Orders
        var orders = new[]
        {
            new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customers[0].Id,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
                Status = "Completed",
                Total = 1029.98m
            },
            new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customers[1].Id,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-3),
                Status = "Completed",
                Total = 379.97m
            },
            new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customers[2].Id,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-1),
                Status = "Pending",
                Total = 299.99m
            },
        };
        db.Orders.AddRange(orders);
        await db.SaveChangesAsync();

        // Order Items
        var orderItems = new[]
        {
            new OrderItem { Id = Guid.NewGuid(), OrderId = orders[0].Id, ProductId = products[0].Id, Quantity = 1, UnitPrice = 999.99m },
            new OrderItem { Id = Guid.NewGuid(), OrderId = orders[0].Id, ProductId = products[1].Id, Quantity = 1, UnitPrice = 29.99m },
            new OrderItem { Id = Guid.NewGuid(), OrderId = orders[1].Id, ProductId = products[2].Id, Quantity = 1, UnitPrice = 79.99m },
            new OrderItem { Id = Guid.NewGuid(), OrderId = orders[1].Id, ProductId = products[4].Id, Quantity = 3, UnitPrice = 199.99m },
            new OrderItem { Id = Guid.NewGuid(), OrderId = orders[2].Id, ProductId = products[3].Id, Quantity = 1, UnitPrice = 299.99m },
        };
        db.OrderItems.AddRange(orderItems);
        await db.SaveChangesAsync();
    }
}
