namespace Bff.Api.Tests.Integration;

using System.Net;
using System.Text.Json;
using Bff.Api.Tests.Mocks;
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
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(1, body.RootElement.GetProperty("id").GetInt32());
        Assert.AreEqual("Portátil", body.RootElement.GetProperty("name").GetString());
        Assert.AreEqual("Equipo para trabajar.", body.RootElement.GetProperty("description").GetString());
        Assert.AreEqual(899.90m, body.RootElement.GetProperty("price").GetDecimal());
        Assert.AreEqual("899,90 EUR", body.RootElement.GetProperty("displayPrice").GetString());
        Assert.AreEqual(availability, body.RootElement.GetProperty("availability").GetString());
        Assert.AreEqual(canBuy, body.RootElement.GetProperty("canBuy").GetBoolean());
        Assert.IsFalse(body.RootElement.TryGetProperty("supplierCost", out _));
        Assert.IsFalse(body.RootElement.TryGetProperty("availableUnits", out _));
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
        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.AreEqual("El id debe ser mayor que cero.", body.RootElement.GetProperty("title").GetString());
        Assert.HasCount(0, backend.Invocations);
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
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(404, body.RootElement.GetProperty("status").GetInt32());
        Assert.AreEqual("Producto no encontrado.", body.RootElement.GetProperty("title").GetString());
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
        Assert.AreEqual(expectedStatus, (int)response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.AreEqual(expectedStatus, body.RootElement.GetProperty("status").GetInt32());
        Assert.AreEqual(expectedStatus switch
        {
            504 => "El servicio de datos tardó demasiado en responder.",
            502 => "No se pudo obtener la información del producto.",
            _ => "Ocurrió un error inesperado."
        }, body.RootElement.GetProperty("title").GetString());
        Assert.IsFalse(responseText.Contains(internalDetail, StringComparison.Ordinal));
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
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
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
        Assert.AreEqual(HttpStatusCode.BadGateway, response.StatusCode);
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
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("ok", body.RootElement.GetProperty("status").GetString());
        Assert.HasCount(0, backend.Invocations);
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
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        StringAssert.Contains(html, "/bff/products/");
        Assert.HasCount(0, backend.Invocations);
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
