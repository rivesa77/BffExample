namespace Bff.Api.Interfaces;

using Bff.Api.Models;
using Bff.Api.Requests;

public interface IProductCreationService
{
    Task<CreatedProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);
}
