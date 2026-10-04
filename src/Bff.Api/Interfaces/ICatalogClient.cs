namespace Bff.Api.Interfaces;

using Bff.Api.Models;
using Bff.Api.Requests;

public interface ICatalogClient
{
    Task<CatalogProduct?> GetProductAsync(int id, CancellationToken cancellationToken);
    Task<CatalogProduct> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken);
}
