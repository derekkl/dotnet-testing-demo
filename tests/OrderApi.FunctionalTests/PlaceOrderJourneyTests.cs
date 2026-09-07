using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace OrderApi.FunctionalTests;

// Functional/E2E tests: exercise the real, separately-running app purely
// over HTTP, the same way an actual client would. No shortcuts, no shared
// C# types with the app -- these tests only know the JSON wire contract.
// They describe a user-facing journey rather than a single endpoint.
public class PlaceOrderJourneyTests : IClassFixture<ApiProcessFixture>
{
    private readonly HttpClient _client;

    public PlaceOrderJourneyTests(ApiProcessFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task CustomerCanPlaceAnOrderAndRetrieveItAfterward()
    {
        var placeResponse = await _client.PostAsJsonAsync("/orders", new
        {
            customerId = "e2e-customer",
            items = new[] { new { sku = "WIDGET", quantity = 4, unitPrice = 30.00m } },
        });

        Assert.Equal(HttpStatusCode.Created, placeResponse.StatusCode);

        var placed = await placeResponse.Content.ReadFromJsonAsync<JsonElement>();
        var orderId = placed.GetProperty("id").GetString();
        var total = placed.GetProperty("total").GetDecimal();

        // $30 x 4 = $120, which crosses the $100 discount threshold -> 10% off -> $108.
        Assert.Equal(108.00m, total);

        var getResponse = await _client.GetAsync($"/orders/{orderId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var fetched = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(orderId, fetched.GetProperty("id").GetString());
        Assert.Equal(total, fetched.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task RejectsAnOrderWithNoItems()
    {
        var response = await _client.PostAsJsonAsync("/orders", new
        {
            customerId = "e2e-customer",
            items = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
