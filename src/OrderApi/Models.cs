namespace OrderApi;

public record OrderItem(string Sku, int Quantity, decimal UnitPrice);

public record PlaceOrderRequest(string CustomerId, List<OrderItem> Items);

public class Order
{
    public required string Id { get; init; }
    public required string CustomerId { get; init; }
    public required List<OrderItem> Items { get; init; }
    public required decimal Subtotal { get; init; }
    public required decimal Discount { get; init; }
    public required decimal Total { get; init; }
}
