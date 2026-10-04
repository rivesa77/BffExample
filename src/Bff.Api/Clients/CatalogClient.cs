namespace Bff.Api.Clients;

using System.Net;
using Bff.Api.Interfaces;
using Bff.Api.Models;
using Bff.Api.Requests;
using FluentValidation;

public sealed class CatalogClient(
    HttpClient httpClient,
    IValidator<int> idValidator,
    IValidator<CreateProductRequest> createProductValidator) : ICatalogClient
{
    public async Task<CatalogProduct?> GetProductAsync(int id, CancellationToken cancellationToken)
    {
        await idValidator.ValidateAndThrowAsync(id, cancellationToken);

        using var response = await httpClient.GetAsync($"catalog/products/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogProduct>(cancellationToken)
            ?? throw new InvalidDataException("El catálogo devolvió un cuerpo vacío.");
    }

    public async Task<CatalogProduct> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        await createProductValidator.ValidateAndThrowAsync(request, cancellationToken);

        using var response = await httpClient.PostAsJsonAsync("catalog/products", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogProduct>(cancellationToken)
            ?? throw new InvalidDataException("El catálogo devolvió un cuerpo vacío al crear el producto.");
    }
}
