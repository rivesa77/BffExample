namespace Bff.Api.Tests.Mocks;

using Bff.Api.Clients;
using Bff.Api.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public sealed class BffWebApplicationFactory(Mock<HttpMessageHandler> backend)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Backends:CatalogBaseUrl"] = "http://backend.test/",
                ["Backends:InventoryBaseUrl"] = "http://backend.test/"
            }));
        builder.ConfigureTestServices(services =>
        {
            // Los clientes reales usan un transporte mockeado en lugar de la red.
            services.AddHttpClient<ICatalogClient, CatalogClient>()
                .ConfigurePrimaryHttpMessageHandler(() => backend.Object);
            services.AddHttpClient<IInventoryClient, InventoryClient>()
                .ConfigurePrimaryHttpMessageHandler(() => backend.Object);
        });
    }
}
