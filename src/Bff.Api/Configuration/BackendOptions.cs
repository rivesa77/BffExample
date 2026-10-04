namespace Bff.Api.Configuration;

public sealed class BackendOptions
{
    public string CatalogBaseUrl { get; init; } = "";
    public string InventoryBaseUrl { get; init; } = "";

    public static bool IsValidUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
        && value.EndsWith('/');
}
