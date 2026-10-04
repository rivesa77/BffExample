namespace Bff.Api.Clients;

using System.Net;
using Bff.Api.Interfaces;
using Bff.Api.Models;

public sealed class CatalogClient(HttpClient httpClient) : ICatalogClient
{
    public async Task<CatalogProduct?> GetProductAsync(int id, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"catalog/products/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogProduct>(cancellationToken)
            ?? throw new InvalidDataException("El catálogo devolvió un cuerpo vacío.");
    }
}
