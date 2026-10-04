namespace Bff.Api.FluentTests.Integration;

using System.Net;
using System.Text.Json;
using Bff.Api.Models;
using Bff.Api.FluentTests.Mocks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Integration")]
public sealed class ProductEndpointsTests
{
    [TestMethod]
    [DataRow(7, true, "Disponible")]
    [DataRow(0, false, "Agotado")]
    public async Task GetProduct_WithBackendResponses_ReturnsScreenDto(
        int availableUnits, bool canBuy, string availability)
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.OK,
            ContentType = "application/json",
            Product = new ProductPageDto(1, "Portátil", "Equipo para trabajar.", 899.90m,
                "899,90 EUR", availability, canBuy)
        };
        string[] expectedProperties = ["id", "name", "description", "price", "displayPrice", "availability", "canBuy"];
        var backend = CreateBackend(availableUnits);
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/1");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Product = body.RootElement.Deserialize<ProductPageDto>(JsonSerializerOptions.Web)
        };
        var actualProperties = body.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
        actualProperties.Should().BeEquivalentTo(expectedProperties);
        BackendHttpMock.VerifyGet(backend, "/catalog/products/1");
        BackendHttpMock.VerifyGet(backend, "/inventory/products/1");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task GetProduct_WithInvalidId_Returns400WithoutCallingBackends(int id)
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.BadRequest,
            ContentType = "application/problem+json",
            Status = 400,
            Title = "El id debe ser mayor que cero."
        };
        var backend = CreateBackend();
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/bff/products/{id}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Status = body.RootElement.GetProperty("status").GetInt32(),
            Title = body.RootElement.GetProperty("title").GetString()
        };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
        backend.Invocations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetProduct_WhenProductDoesNotExist_Returns404ProblemDetails()
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.NotFound,
            ContentType = "application/problem+json",
            Status = 404,
            Title = "Producto no encontrado."
        };
        var backend = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/999");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Status = body.RootElement.GetProperty("status").GetInt32(),
            Title = body.RootElement.GetProperty("title").GetString()
        };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    [DataRow("http-error", 502)]
    [DataRow("connection-error", 502)]
    [DataRow("invalid-json", 502)]
    [DataRow("null-body", 502)]
    [DataRow("timeout", 504)]
    [DataRow("unexpected-error", 500)]
    public async Task GetProduct_WithBackendFailure_ReturnsSafeProblemDetails(string failure, int expectedStatus)
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = (HttpStatusCode)expectedStatus,
            ContentType = "application/problem+json",
            Status = expectedStatus,
            Title = expectedStatus switch
            {
                504 => "El servicio de datos tardó demasiado en responder.",
                502 => "No se pudo obtener la información del producto.",
                _ => "Ocurrió un error inesperado."
            }
        };
        const string internalDetail = "Detalle interno que no debe publicarse.";
        var backend = BackendHttpMock.Create((_, _) => failure switch
        {
            "http-error" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            "connection-error" => Task.FromException<HttpResponseMessage>(new HttpRequestException(internalDetail)),
            "invalid-json" => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "{invalid-json")),
            "null-body" => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "null")),
            "timeout" => Task.FromException<HttpResponseMessage>(new TaskCanceledException(internalDetail)),
            _ => Task.FromException<HttpResponseMessage>(new InvalidOperationException(internalDetail))
        });
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/1");
        var responseText = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(responseText);
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Status = body.RootElement.GetProperty("status").GetInt32(),
            Title = body.RootElement.GetProperty("title").GetString()
        };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
        responseText.Should().NotContain(internalDetail);
    }

    [TestMethod]
    public async Task GetProduct_WhenExistingProductHasNoInventory_Returns502()
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.BadGateway,
            ContentType = "application/problem+json",
            Status = 502,
            Title = "No se pudo obtener la información del producto."
        };
        var backend = BackendHttpMock.Create((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.StartsWith("/catalog/", StringComparison.Ordinal)
                ? CatalogResponse()
                : new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/1");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Status = body.RootElement.GetProperty("status").GetInt32(),
            Title = body.RootElement.GetProperty("title").GetString()
        };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    public async Task GetProduct_WhenCatalogIsNotFoundButInventoryFails_Returns502InsteadOf404()
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.BadGateway,
            ContentType = "application/problem+json",
            Status = 502,
            Title = "No se pudo obtener la información del producto."
        };
        var backend = BackendHttpMock.Create((request, _) => Task.FromResult(new HttpResponseMessage(
            request.RequestUri!.AbsolutePath.StartsWith("/catalog/", StringComparison.Ordinal)
                ? HttpStatusCode.NotFound
                : HttpStatusCode.ServiceUnavailable)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/999");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Status = body.RootElement.GetProperty("status").GetInt32(),
            Title = body.RootElement.GetProperty("title").GetString()
        };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
        BackendHttpMock.VerifyGet(backend, "/catalog/products/999");
        BackendHttpMock.VerifyGet(backend, "/inventory/products/999");
    }

    [TestMethod]
    public async Task GetHealth_ReturnsOkWithoutCallingBackends()
    {
        // Arrange
        var expectedResult = new { StatusCode = HttpStatusCode.OK, Status = "ok" };
        var backend = CreateBackend();
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            Status = body.RootElement.GetProperty("status").GetString()
        };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
        backend.Invocations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetHome_ReturnsHtmlWithoutCallingBackends()
    {
        // Arrange
        var expectedResult = new { StatusCode = HttpStatusCode.OK, ContentType = "text/html" };
        const string expectedContentFragment = "/bff/products/";
        var backend = CreateBackend();
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType
        };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
        html.Should().Contain(expectedContentFragment);
        backend.Invocations.Should().BeEmpty();
    }

    private static Mock<HttpMessageHandler> CreateBackend(int availableUnits = 7) =>
        BackendHttpMock.Create((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.StartsWith("/catalog/", StringComparison.Ordinal)
                ? CatalogResponse()
                : BackendHttpMock.Json(HttpStatusCode.OK,
                    $$"""{"productId":1,"availableUnits":{{availableUnits}}}""")));

    private static HttpResponseMessage CatalogResponse() =>
        BackendHttpMock.Json(HttpStatusCode.OK,
            """{"id":1,"name":"Portátil","description":"Equipo para trabajar.","price":899.90,"currency":"EUR","supplierCost":620}""");
}
