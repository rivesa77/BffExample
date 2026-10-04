namespace Bff.Api;

using Bff.Api.Clients;
using Bff.Api.Configuration;
using Bff.Api.Endpoints;
using Bff.Api.ExceptionHandlers;
using Bff.Api.Interfaces;
using Bff.Api.Services;
using Microsoft.Extensions.Options;

public sealed class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<BackendExceptionHandler>();
        builder.Services.AddOptions<BackendOptions>()
            .BindConfiguration("Backends")
            .Validate(options => BackendOptions.IsValidUrl(options.CatalogBaseUrl)
                && BackendOptions.IsValidUrl(options.InventoryBaseUrl),
                "Las URLs de los backends deben ser absolutas, HTTP(S) y terminar en /.")
            .ValidateOnStart();

        // Adapter: cada cliente traduce HTTP a un contrato que entiende la fachada.
        builder.Services.AddHttpClient<ICatalogClient, CatalogClient>((services, client) =>
        {
            client.BaseAddress = new Uri(services.GetRequiredService<IOptions<BackendOptions>>().Value.CatalogBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        builder.Services.AddHttpClient<IInventoryClient, InventoryClient>((services, client) =>
        {
            client.BaseAddress = new Uri(services.GetRequiredService<IOptions<BackendOptions>>().Value.InventoryBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        });
        builder.Services.AddScoped<IProductPageService, ProductPageService>();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }));
        app.MapProductEndpoints();
        app.Run();
    }
}
