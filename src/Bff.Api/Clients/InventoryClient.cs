namespace Bff.Api.Clients;

using System.Net;
using Bff.Api.Interfaces;
using Bff.Api.Models;
using FluentValidation;

public sealed class InventoryClient(
    HttpClient httpClient,
    IValidator<int> validator) : IInventoryClient
{
    public async Task<InventoryStock?> GetStockAsync(int productId, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(productId, cancellationToken);

        using var response = await httpClient.GetAsync($"inventory/products/{productId}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InventoryStock>(cancellationToken)
            ?? throw new InvalidDataException("El inventario devolvió un cuerpo vacío.");
    }
}
