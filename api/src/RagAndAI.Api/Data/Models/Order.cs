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
