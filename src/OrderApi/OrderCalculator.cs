namespace OrderApi;

public class OrderCalculator
{
    private readonly IDiscountService _discountService;

    public OrderCalculator(IDiscountService discountService)
    {
        _discountService = discountService;
    }

    public Order Calculate(string id, PlaceOrderRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
            throw new ArgumentException("Order must contain at least one item.");

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
                throw new ArgumentException($"Quantity for {item.Sku} must be greater than zero.");
            if (item.UnitPrice < 0)
                throw new ArgumentException($"Unit price for {item.Sku} cannot be negative.");
        }

        var subtotal = request.Items.Sum(i => i.Quantity * i.UnitPrice);
        var discountRate = _discountService.GetDiscountRate(subtotal);
        var discount = Math.Round(subtotal * discountRate, 2);
        var total = subtotal - discount;

        return new Order
        {
            Id = id,
            CustomerId = request.CustomerId,
            Items = request.Items,
            Subtotal = subtotal,
            Discount = discount,
            Total = total,
        };
    }
}
