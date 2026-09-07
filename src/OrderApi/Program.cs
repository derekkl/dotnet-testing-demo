using System.Collections.Concurrent;
using OrderApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDiscountService, DiscountService>();
builder.Services.AddSingleton<OrderCalculator>();
builder.Services.AddSingleton<ConcurrentDictionary<string, Order>>();

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/orders", (PlaceOrderRequest request, OrderCalculator calculator, ConcurrentDictionary<string, Order> store) =>
{
    try
    {
        var id = Guid.NewGuid().ToString();
        var order = calculator.Calculate(id, request);
        store[id] = order;
        return Results.Created($"/orders/{id}", order);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/orders/{id}", (string id, ConcurrentDictionary<string, Order> store) =>
{
    return store.TryGetValue(id, out var order)
        ? Results.Ok(order)
        : Results.NotFound();
});

app.Run();

// Exposes the implicit Program class generated from top-level statements so
// WebApplicationFactory<Program> (used by the integration tests) can see it.
public partial class Program { }
