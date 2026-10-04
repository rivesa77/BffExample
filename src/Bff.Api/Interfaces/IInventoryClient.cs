namespace Bff.Api.Interfaces;

using Bff.Api.Models;

public interface IInventoryClient
{
    Task<InventoryStock?> GetStockAsync(int productId, CancellationToken cancellationToken);
}
