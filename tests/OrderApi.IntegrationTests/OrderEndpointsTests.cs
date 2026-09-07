using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderApi;
using Xunit;

namespace OrderApi.IntegrationTests;

// Integration tests: WebApplicationFactory boots the real ASP.NET Core
// pipeline -- routing, model binding, dependency injection, the actual
// DiscountService implementation -- all wired together exactly as in
// production. But it's all in-memory: there's no real OS process, no real
// TCP socket, no separate `dotnet run`. This layer catches wiring bugs that
// pure unit tests can't see (a route that's misspelled, a service that
// isn't registered, a model that doesn't bind), while still running fast
// enough to use constantly during development.
public class OrderEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrderEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PostOrders_ValidRequest_Returns201WithComputedTotal()
    {
        var request = new PlaceOrderRequest("customer-1", new List<OrderItem>
        {
            new("WIDGET", 2, 10.00m),
        });

        var response = await _client.PostAsJsonAsync("/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<Order>();
        Assert.NotNull(order);
        Assert.Equal(20.00m, order!.Total);
    }

    [Fact]
    public async Task PostOrders_EmptyItems_Returns400()
    {
        var request = new PlaceOrderRequest("customer-1", new List<OrderItem>());

        var response = await _client.PostAsJsonAsync("/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_AfterPlacing_ReturnsMatchingOrder()
    {
        var request = new PlaceOrderRequest("customer-2", new List<OrderItem>
        {
            new("GADGET", 1, 25.00m),
        });

        var createResponse = await _client.PostAsJsonAsync("/orders", request);
        var created = await createResponse.Content.ReadFromJsonAsync<Order>();

        var getResponse = await _client.GetAsync($"/orders/{created!.Id}");
        var fetched = await getResponse.Content.ReadFromJsonAsync<Order>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.Id, fetched!.Id);
        Assert.Equal(created.Total, fetched.Total);
    }

    [Fact]
    public async Task GetOrder_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
