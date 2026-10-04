namespace Bff.Api.Services;

using Bff.Api.Interfaces;
using Bff.Api.Models;
using Bff.Api.Requests;
using FluentValidation;

// Facade: valida la entrada, delega el alta y prepara el contrato público.
public sealed class ProductCreationService(
    ICatalogClient catalogClient,
    IValidator<CreateProductRequest> validator) : IProductCreationService
{
    public async Task<CreatedProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(request, cancellationToken);

        var product = await catalogClient.CreateProductAsync(request, cancellationToken);
        return new CreatedProductDto(product.Id, product.Name, product.Description, product.Price, product.Currency);
    }
}
