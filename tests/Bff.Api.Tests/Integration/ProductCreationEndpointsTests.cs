namespace Bff.Api.Tests.Integration;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Bff.Api.Models;
using Bff.Api.Requests;
using Bff.Api.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Integration")]
public sealed class ProductCreationEndpointsTests
{
    [TestMethod]
    public async Task PostProduct_WithValidInput_Returns201AndBffLocation()
    {
        // Arrange
        var expectedRequest = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.Created,
            ContentType = "application/json",
            Location = "/bff/products/3",
            Product = new CreatedProductDto(3, "Ratón", "Ratón inalámbrico.", 35.95m, "EUR")
        };
        string[] expectedProperties = ["id", "name", "description", "price", "currency"];
        CreateProductRequest? sentRequest = null;
        var backend = BackendHttpMock.Create(async (request, token) =>
        {
            sentRequest = await request.Content!.ReadFromJsonAsync<CreateProductRequest>(token);
            return BackendHttpMock.Json(HttpStatusCode.Created,
                """{"id":3,"name":"Ratón","description":"Ratón inalámbrico.","price":35.95,"currency":"EUR","supplierCost":10}""");
        });
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync("/bff/products", expectedRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Location = response.Headers.Location?.ToString(),
            Product = body.RootElement.Deserialize<CreatedProductDto>(JsonSerializerOptions.Web)
        };
        var actualProperties = body.RootElement.EnumerateObject().Select(property => property.Name).ToArray();

        // Assert
        Assert.AreEqual<object>(expectedResult, actualResult);
        Assert.AreEqual(expectedRequest, sentRequest);
        CollectionAssert.AreEquivalent(expectedProperties, actualProperties);
        BackendHttpMock.VerifyPost(backend, "/catalog/products");
    }

    [TestMethod]
    [DataRow("""{"name":"","description":"Descripción","price":10,"currency":"EUR","initialStock":7}""", "Name", "El nombre es obligatorio.")]
    [DataRow("""{"name":"Ratón","description":"","price":10,"currency":"EUR","initialStock":7}""", "Description", "La descripción es obligatoria.")]
    [DataRow("""{"name":"Ratón","description":"Descripción","price":-1,"currency":"EUR","initialStock":7}""", "Price", "El precio no puede ser negativo.")]
    [DataRow("""{"name":"Ratón","description":"Descripción","currency":"EUR","initialStock":7}""", "Price", "El precio es obligatorio.")]
    [DataRow("""{"name":"Ratón","description":"Descripción","price":10,"currency":"eur","initialStock":7}""", "Currency", "La moneda debe contener exactamente tres letras mayúsculas.")]
    [DataRow("""{"name":"Ratón","description":"Descripción","price":10,"currency":"EUR","initialStock":-1}""", "InitialStock", "Las existencias iniciales no pueden ser negativas.")]
    public async Task PostProduct_WithInvalidInput_Returns400WithFieldErrorWithoutCallingBackend(
        string json, string propertyName, string errorMessage)
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.BadRequest,
            ContentType = "application/problem+json",
            Status = 400,
            Title = "Los datos de entrada no son válidos."
        };
        string[] expectedErrorProperties = [propertyName];
        string[] expectedMessages = [errorMessage];
        var backend = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PostAsync("/bff/products", content);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Status = body.RootElement.GetProperty("status").GetInt32(),
            Title = body.RootElement.GetProperty("title").GetString()
        };
        var errors = body.RootElement.GetProperty("errors");
        var actualErrorProperties = errors.EnumerateObject().Select(property => property.Name).ToArray();
        var actualMessages = errors.GetProperty(propertyName).EnumerateArray().Select(value => value.GetString()).ToArray();

        // Assert
        Assert.AreEqual<object>(expectedResult, actualResult);
        CollectionAssert.AreEquivalent(expectedErrorProperties, actualErrorProperties);
        CollectionAssert.AreEqual(expectedMessages, actualMessages);
        backend.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task PostProduct_WithSeveralInvalidFields_ReturnsAllFieldErrorsWithoutCallingBackend()
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.BadRequest, Status = 400,
            Title = "Los datos de entrada no son válidos."
        };
        string[] expectedProperties = ["Name", "Description", "Price", "Currency", "InitialStock"];
        var backend = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();
        var request = new CreateProductRequest("", "", null, "", -1);

        // Act
        using var response = await client.PostAsJsonAsync("/bff/products", request);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            Status = body.RootElement.GetProperty("status").GetInt32(),
            Title = body.RootElement.GetProperty("title").GetString()
        };
        var actualProperties = body.RootElement.GetProperty("errors")
            .EnumerateObject().Select(property => property.Name).ToArray();

        // Assert
        Assert.AreEqual<object>(expectedResult, actualResult);
        CollectionAssert.AreEquivalent(expectedProperties, actualProperties);
        backend.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("{invalid-json")]
    [DataRow("null")]
    public async Task PostProduct_WithUnreadableBody_Returns400WithoutCallingBackend(string json)
    {
        // Arrange
        var expectedResult = HttpStatusCode.BadRequest;
        var backend = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        using var response = await client.PostAsync("/bff/products", content);
        var actualResult = response.StatusCode;

        // Assert
        Assert.AreEqual(expectedResult, actualResult);
        backend.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("http-error", 502)]
    [DataRow("connection-error", 502)]
    [DataRow("invalid-json", 502)]
    [DataRow("null-body", 502)]
    [DataRow("timeout", 504)]
    public async Task PostProduct_WithBackendFailure_ReturnsSafeProblemDetails(string failure, int expectedStatus)
    {
        // Arrange
        var expectedResult = new
        {
            StatusCode = (HttpStatusCode)expectedStatus,
            ContentType = "application/problem+json",
            Status = expectedStatus,
            Title = expectedStatus == 504
                ? "El servicio de datos tardó demasiado en responder."
                : "No se pudo obtener la información del producto."
        };
        const string internalDetail = "Detalle interno que no debe publicarse.";
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var backend = BackendHttpMock.Create((_, _) => failure switch
        {
            "http-error" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            "connection-error" => Task.FromException<HttpResponseMessage>(new HttpRequestException(internalDetail)),
            "invalid-json" => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.Created, "{invalid-json")),
            "null-body" => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.Created, "null")),
            _ => Task.FromException<HttpResponseMessage>(new TaskCanceledException(internalDetail))
        });
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync("/bff/products", request);
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
        Assert.AreEqual<object>(expectedResult, actualResult);
        Assert.IsFalse(responseText.Contains(internalDetail, StringComparison.Ordinal));
        BackendHttpMock.VerifyPost(backend, "/catalog/products");
    }

    [TestMethod]
    public async Task PostProduct_WithBackendValues_ReturnsOutputWithoutValidation()
    {
        // Arrange
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var expectedResult = new
        {
            StatusCode = HttpStatusCode.Created,
            Product = new CreatedProductDto(3, "", "", -1m, "eur")
        };
        var backend = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.Created,
            """{"id":3,"name":"","description":"","price":-1,"currency":"eur"}""")));
        using var factory = new BffWebApplicationFactory(backend);
        using var client = factory.CreateClient();

        // Act
        using var response = await client.PostAsJsonAsync("/bff/products", request);
        var actualResult = new
        {
            StatusCode = response.StatusCode,
            Product = await response.Content.ReadFromJsonAsync<CreatedProductDto>()
        };

        // Assert
        Assert.AreEqual<object>(expectedResult, actualResult);
        BackendHttpMock.VerifyPost(backend, "/catalog/products");
    }
}

