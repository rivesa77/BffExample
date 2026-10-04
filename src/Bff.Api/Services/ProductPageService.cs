namespace Bff.Api.Services;

using System.Globalization;
using Bff.Api.Interfaces;
using Bff.Api.Models;
using FluentValidation;

// Facade + agregación: la pantalla hace una petición y recibe su modelo completo.
public sealed class ProductPageService(
    ICatalogClient catalogClient,
    IInventoryClient inventoryClient,
    IValidator<int> validator) : IProductPageService
{
    public async Task<ProductPageDto?> GetAsync(int id, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(id, cancellationToken);

        // Las consultas son independientes y se ejecutan en paralelo.
        var productTask = catalogClient.GetProductAsync(id, cancellationToken);
        var stockTask = inventoryClient.GetStockAsync(id, cancellationToken);
        await Task.WhenAll(productTask, stockTask);

        var product = await productTask;
        if (product is null)
            return null;

        var stock = await stockTask
            ?? throw new InvalidDataException("El producto existe, pero no tiene registro de inventario.");

        var canBuy = stock.AvailableUnits > 0;
        return new ProductPageDto(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            $"{product.Price.ToString("N2", CultureInfo.GetCultureInfo("es-ES"))} {product.Currency}",
            canBuy ? "Disponible" : "Agotado",
            canBuy);
    }
}
