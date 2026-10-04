namespace Demo.Backend.Models;

internal sealed record Product(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    decimal SupplierCost);
