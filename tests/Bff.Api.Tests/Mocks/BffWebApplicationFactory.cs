namespace Bff.Api.Tests.Mocks;

using Bff.Api.Configuration;
using Bff.Api.Interfaces;
using Bff.Api.Clients;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;

public sealed class BffWebApplicationFactory(Mock<HttpMessageHandler> backend)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Production");
        builder.ConfigureTestServices(services =>
        {
            // URLs fijas para que la configuración externa no afecte a las pruebas.
            services.Configure<BackendOptions>(options => { });
            services.AddOptions<BackendOptions>().Configure(options => { });
            services.AddHttpClient<ICatalogClient, CatalogClient>()
                .ConfigurePrimaryHttpMessageHandler(() => backend.Object);
            services.AddHttpClient<IInventoryClient, InventoryClient>()
                .ConfigurePrimaryHttpMessageHandler(() => backend.Object);
        });
    }
}
