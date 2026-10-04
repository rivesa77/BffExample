namespace Bff.Api.Requests;

public sealed record CreateProductRequest(
    string? Name,
    string? Description,
    decimal? Price,
    string? Currency,
    int InitialStock = 0);
