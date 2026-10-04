namespace Demo.Backend.Requests;

internal sealed record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int InitialStock = 0);
