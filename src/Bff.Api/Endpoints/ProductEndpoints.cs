namespace Bff.Api.Endpoints;

using Bff.Api.Interfaces;
using Bff.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        app.MapGet("/bff/products/{id:int}", GetProductAsync);
    }

    private static async Task<Results<Ok<ProductPageDto>, ProblemHttpResult>> GetProductAsync(
        int id, IProductPageService service, CancellationToken cancellationToken)
    {
        var product = await service.GetAsync(id, cancellationToken);
        return product is null
            ? TypedResults.Problem(statusCode: 404, title: "Producto no encontrado.")
            : TypedResults.Ok(product);
    }
}
