namespace Bff.Api.Tests.Clients;

using System.Net;
using System.Text.Json;
using Bff.Api.Clients;
using Bff.Api.Models;
using Bff.Api.Tests.Mocks;
using Bff.Api.Validators;
using FluentValidation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Unit")]
public sealed class CatalogClientTests
{
    [TestMethod]
    public async Task GetProductAsync_WithValidResponse_DeserializesAndUsesCatalogRoute()
    {
        // Arrange
        var expectedResult = new CatalogProduct(4, "Teclado", "Compacto", 49.90m, "EUR");
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(
            HttpStatusCode.OK,
            """{"id":4,"name":"Teclado","description":"Compacto","price":49.90,"currency":"EUR","supplierCost":25}""")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator());

        // Act
        var product = await client.GetProductAsync(4, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, product);
        BackendHttpMock.VerifyGet(handler, "/catalog/products/4");
    }

    [TestMethod]
    public async Task GetProductAsync_WithNotFound_ReturnsNull()
    {
        // Arrange
        CatalogProduct? expectedResult = null;
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator());

        // Act
        var product = await client.GetProductAsync(999, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, product);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public async Task GetProductAsync_WithBackendError_ThrowsHttpRequestException(HttpStatusCode status)
    {
        // Arrange
        var expectedStatusCode = status;
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetProductAsync(1, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(action);
        Assert.AreEqual(expectedStatusCode, exception.StatusCode);
    }

    [TestMethod]
    public async Task GetProductAsync_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "{invalid-json")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetProductAsync(1, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<JsonException>(action);
    }

    [TestMethod]
    public async Task GetProductAsync_WithJsonNull_ThrowsInvalidDataException()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "null")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetProductAsync(1, CancellationToken.None);

        // Assert
        await Assert.ThrowsExactlyAsync<InvalidDataException>(action);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public async Task GetProductAsync_WithInvalidId_ThrowsValidationExceptionWithoutSendingHttp(int id)
    {
        // Arrange
        var expectedResult = new { PropertyName = "Id", ErrorMessage = "El id debe ser mayor que cero." };
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetProductAsync(id, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<ValidationException>(action);
        var error = exception.Errors.Single();
        var actualResult = new { error.PropertyName, error.ErrorMessage };
        Assert.AreEqual(expectedResult, actualResult);
        handler.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GetProductAsync_WithPositiveInput_ReturnsBackendDataWithoutValidation()
    {
        // Arrange
        var expectedResult = new CatalogProduct(2, "", "", -1m, "eur");
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(
            HttpStatusCode.OK, """{"id":2,"name":"","description":"","price":-1,"currency":"eur"}""")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator());

        // Act
        var actualResult = await client.GetProductAsync(1, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, actualResult);
        BackendHttpMock.VerifyGet(handler, "/catalog/products/1");
    }

    [TestMethod]
    public async Task GetProductAsync_WhenCanceled_CancelsTheHttpRequest()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var handler = BackendHttpMock.Create(async (_, token) =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetProductAsync(1, cancellation.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(action);
        BackendHttpMock.VerifyGet(handler, "/catalog/products/1");
    }
}
