using Moq;
using OrderApi;
using Xunit;

namespace OrderApi.UnitTests;

// Unit tests: the OrderCalculator is tested in complete isolation. Its one
// dependency (IDiscountService) is mocked, so these tests never touch HTTP,
// the DI container, or ASP.NET Core at all. This is the fastest, cheapest
// layer -- it should be the layer with the most tests.
public class OrderCalculatorTests
{
    private static PlaceOrderRequest MakeRequest(params OrderItem[] items) =>
        new("customer-1", items.ToList());

    [Fact]
    public void Calculate_SingleItem_ComputesCorrectSubtotal()
    {
        var discountMock = new Mock<IDiscountService>();
        discountMock.Setup(d => d.GetDiscountRate(It.IsAny<decimal>())).Returns(0m);
        var calculator = new OrderCalculator(discountMock.Object);

        var order = calculator.Calculate("order-1", MakeRequest(new OrderItem("WIDGET", 2, 10.00m)));

        Assert.Equal(20.00m, order.Subtotal);
        Assert.Equal(0m, order.Discount);
        Assert.Equal(20.00m, order.Total);
    }

    [Fact]
    public void Calculate_AppliesDiscountFromDiscountService()
    {
        var discountMock = new Mock<IDiscountService>();
        discountMock.Setup(d => d.GetDiscountRate(150.00m)).Returns(0.10m);
        var calculator = new OrderCalculator(discountMock.Object);

        var order = calculator.Calculate("order-2", MakeRequest(new OrderItem("WIDGET", 3, 50.00m)));

        Assert.Equal(150.00m, order.Subtotal);
        Assert.Equal(15.00m, order.Discount);
        Assert.Equal(135.00m, order.Total);
        // Confirms the calculator actually consulted the dependency rather than
        // hardcoding a rate -- a check that's only possible because it's mocked.
        discountMock.Verify(d => d.GetDiscountRate(150.00m), Times.Once);
    }

    [Fact]
    public void Calculate_EmptyItems_ThrowsArgumentException()
    {
        var calculator = new OrderCalculator(Mock.Of<IDiscountService>());

        Assert.Throws<ArgumentException>(() => calculator.Calculate("order-3", MakeRequest()));
    }

    [Fact]
    public void Calculate_ZeroQuantity_ThrowsArgumentException()
    {
        var calculator = new OrderCalculator(Mock.Of<IDiscountService>());

        Assert.Throws<ArgumentException>(() =>
            calculator.Calculate("order-4", MakeRequest(new OrderItem("WIDGET", 0, 10.00m))));
    }

    [Fact]
    public void Calculate_NegativeUnitPrice_ThrowsArgumentException()
    {
        var calculator = new OrderCalculator(Mock.Of<IDiscountService>());

        Assert.Throws<ArgumentException>(() =>
            calculator.Calculate("order-5", MakeRequest(new OrderItem("WIDGET", 1, -5.00m))));
    }
}
