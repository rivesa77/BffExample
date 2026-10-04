namespace Demo.Backend.Endpoints;

using Demo.Backend.Models;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        // Dos APIs simuladas en un único proceso para simplificar la ejecución local.
        // Los datos y las reglas de negocio pertenecen al backend, no al BFF.
        var products = new Dictionary<int, Product>
        {
            [1] = new(1, "Portátil", "Portátil de 14 pulgadas para trabajar y estudiar.", 899.90m, "EUR", 620m),
            [2] = new(2, "Teclado", "Teclado mecánico compacto.", 49.90m, "EUR", 25m)
        };
        var inventory = new Dictionary<int, Stock>
        {
            [1] = new(1, 7),
            [2] = new(2, 0)
        };

        app.MapGet("/catalog/products/{id:int}", (int id) =>
            products.TryGetValue(id, out var product) ? Results.Ok(product) : Results.NotFound());
        app.MapGet("/inventory/products/{id:int}", (int id) =>
            inventory.TryGetValue(id, out var stock) ? Results.Ok(stock) : Results.NotFound());
    }
}
