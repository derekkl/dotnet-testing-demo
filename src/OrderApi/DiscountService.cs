namespace OrderApi;

/// <summary>10% off orders of $100 or more; no discount otherwise.</summary>
public class DiscountService : IDiscountService
{
    public decimal GetDiscountRate(decimal subtotal) => subtotal >= 100m ? 0.10m : 0m;
}
