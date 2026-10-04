namespace Bff.Api.Endpoints;

using Bff.Api.Interfaces;
using Bff.Api.Models;
using Bff.Api.Requests;
using Microsoft.AspNetCore.Http.HttpResults;

public static class ProductEndpoints
{
    public static void MapProductEndpoints(this WebApplication app)
    {
        app.MapGet("/bff/products/{id:int}", GetProductAsync);
        app.MapPost("/bff/products", CreateProductAsync);
    }

    private static async Task<Results<Ok<ProductPageDto>, ProblemHttpResult>> GetProductAsync(
        int id, IProductPageService service, CancellationToken cancellationToken)
    {
        var product = await service.GetAsync(id, cancellationToken);
        return product is null
            ? TypedResults.Problem(statusCode: 404, title: "Producto no encontrado.")
            : TypedResults.Ok(product);
    }

    private static async Task<Created<CreatedProductDto>> CreateProductAsync(
        CreateProductRequest request, IProductCreationService service, CancellationToken cancellationToken)
    {
        var product = await service.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/bff/products/{product.Id}", product);
    }
}
