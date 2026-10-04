namespace Bff.Api.Models;

// Contrato orientado a la pantalla web, independiente de los contratos externos.
public sealed record ProductPageDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string DisplayPrice,
    string Availability,
    bool CanBuy);
