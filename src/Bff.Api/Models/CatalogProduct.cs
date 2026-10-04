namespace Bff.Api.Models;

// Contrato externo: SupplierCost existe en el backend, pero no en el DTO del BFF.
public sealed record CatalogProduct(int Id, string Name, string Description, decimal Price, string Currency);
