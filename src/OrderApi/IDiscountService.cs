namespace OrderApi;

public interface IDiscountService
{
    /// <summary>Returns the discount rate (e.g. 0.10 for 10%) to apply to the given subtotal.</summary>
    decimal GetDiscountRate(decimal subtotal);
}
