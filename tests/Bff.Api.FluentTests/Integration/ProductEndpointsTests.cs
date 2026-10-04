namespace Bff.Api.FluentTests.Integration;

using System.Net;
using System.Text.Json;
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
        var backend = CreateBackend(availableUnits);
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/1");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/json");
        body.RootElement.GetProperty("id").GetInt32().Should().Be(1);
        body.RootElement.GetProperty("name").GetString().Should().Be("Portátil");
        body.RootElement.GetProperty("description").GetString().Should().Be("Equipo para trabajar.");
        body.RootElement.GetProperty("price").GetDecimal().Should().Be(899.90m);
        body.RootElement.GetProperty("displayPrice").GetString().Should().Be("899,90 EUR");
        body.RootElement.GetProperty("availability").GetString().Should().Be(availability);
        body.RootElement.GetProperty("canBuy").GetBoolean().Should().Be(canBuy);
        body.RootElement.TryGetProperty("supplierCost", out _).Should().BeFalse();
        body.RootElement.TryGetProperty("availableUnits", out _).Should().BeFalse();
        BackendHttpMock.VerifyGet(backend, "/catalog/products/1");
        BackendHttpMock.VerifyGet(backend, "/inventory/products/1");
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public async Task GetProduct_WithInvalidId_Returns400WithoutCallingBackends(int id)
    {
        // Arrange
        var backend = CreateBackend();
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync($"/bff/products/{id}");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
        body.RootElement.GetProperty("status").GetInt32().Should().Be(400);
        body.RootElement.GetProperty("title").GetString().Should().Be("El id debe ser mayor que cero.");
        backend.Invocations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetProduct_WhenProductDoesNotExist_Returns404ProblemDetails()
    {
        // Arrange
        var backend = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/999");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
        body.RootElement.GetProperty("status").GetInt32().Should().Be(404);
        body.RootElement.GetProperty("title").GetString().Should().Be("Producto no encontrado.");
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

        // Assert
        ((int)response.StatusCode).Should().Be(expectedStatus);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
        body.RootElement.GetProperty("status").GetInt32().Should().Be(expectedStatus);
        body.RootElement.GetProperty("title").GetString().Should().Be(expectedStatus switch
        {
            504 => "El servicio de datos tardó demasiado en responder.",
            502 => "No se pudo obtener la información del producto.",
            _ => "Ocurrió un error inesperado."
        });
        responseText.Should().NotContain(internalDetail);
    }

    [TestMethod]
    public async Task GetProduct_WhenExistingProductHasNoInventory_Returns502()
    {
        // Arrange
        var backend = BackendHttpMock.Create((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.StartsWith("/catalog/", StringComparison.Ordinal)
                ? CatalogResponse()
                : new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/1");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("application/problem+json");
    }

    [TestMethod]
    public async Task GetProduct_WhenCatalogIsNotFoundButInventoryFails_Returns502InsteadOf404()
    {
        // Arrange
        var backend = BackendHttpMock.Create((request, _) => Task.FromResult(new HttpResponseMessage(
            request.RequestUri!.AbsolutePath.StartsWith("/catalog/", StringComparison.Ordinal)
                ? HttpStatusCode.NotFound
                : HttpStatusCode.ServiceUnavailable)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/bff/products/999");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        BackendHttpMock.VerifyGet(backend, "/catalog/products/999");
        BackendHttpMock.VerifyGet(backend, "/inventory/products/999");
    }

    [TestMethod]
    public async Task GetHealth_ReturnsOkWithoutCallingBackends()
    {
        // Arrange
        var backend = CreateBackend();
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/health");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.RootElement.GetProperty("status").GetString().Should().Be("ok");
        backend.Invocations.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetHome_ReturnsHtmlWithoutCallingBackends()
    {
        // Arrange
        var backend = CreateBackend();
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (response.Content.Headers.ContentType?.MediaType).Should().Be("text/html");
        html.Should().Contain("/bff/products/");
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
