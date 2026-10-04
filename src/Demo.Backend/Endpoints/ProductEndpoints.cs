namespace Demo.Backend.Endpoints;

using System.Collections.Concurrent;
using Demo.Backend.Models;
using Demo.Backend.Requests;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        // Dos APIs simuladas en un único proceso para simplificar la ejecución local.
        // Los datos y las reglas de negocio pertenecen al backend, no al BFF.
        var products = new ConcurrentDictionary<int, Product>
        {
            [1] = new(1, "Portátil", "Portátil de 14 pulgadas para trabajar y estudiar.", 899.90m, "EUR", 620m),
            [2] = new(2, "Teclado", "Teclado mecánico compacto.", 49.90m, "EUR", 25m)
        };
        var inventory = new ConcurrentDictionary<int, Stock>
        {
            [1] = new(1, 7),
            [2] = new(2, 0)
        };
        var lastProductId = 2;

        app.MapPost("/catalog/products", (CreateProductRequest request) =>
        {
            var id = Interlocked.Increment(ref lastProductId);
            var product = new Product(id, request.Name, request.Description, request.Price, request.Currency, 0m);
            // La demo crea el inventario antes de publicar el producto en el catálogo.
            inventory[id] = new Stock(id, request.InitialStock);
            products[id] = product;
            return TypedResults.Created($"/catalog/products/{id}", product);
        });

        app.MapGet("/catalog/products/{id:int}", (int id) =>
            products.TryGetValue(id, out var product) ? Results.Ok(product) : Results.NotFound());
        app.MapGet("/inventory/products/{id:int}", (int id) =>
            inventory.TryGetValue(id, out var stock) ? Results.Ok(stock) : Results.NotFound());
    }
}
