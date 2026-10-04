namespace Bff.Api.Models;

public sealed record CreatedProductDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string Currency);
