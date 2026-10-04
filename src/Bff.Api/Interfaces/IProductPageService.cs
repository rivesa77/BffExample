namespace Bff.Api.Interfaces;

using Bff.Api.Models;

public interface IProductPageService
{
    Task<ProductPageDto?> GetAsync(int id, CancellationToken cancellationToken);
}
