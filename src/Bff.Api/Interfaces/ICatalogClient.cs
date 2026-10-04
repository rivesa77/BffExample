namespace Bff.Api.Interfaces;

using Bff.Api.Models;

public interface ICatalogClient
{
    Task<CatalogProduct?> GetProductAsync(int id, CancellationToken cancellationToken);
}
