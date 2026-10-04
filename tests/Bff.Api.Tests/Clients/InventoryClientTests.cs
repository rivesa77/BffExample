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
public sealed class InventoryClientTests
{
    [TestMethod]
    public async Task GetStockAsync_WithValidResponse_DeserializesAndUsesInventoryRoute()
    {
        // Arrange
        var expectedResult = new InventoryStock(4, 7);
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(
            HttpStatusCode.OK, """{"productId":4,"availableUnits":7}""")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient, new ProductIdValidator());

        // Act
        var stock = await client.GetStockAsync(4, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, stock);
        BackendHttpMock.VerifyGet(handler, "/inventory/products/4");
    }

    [TestMethod]
    public async Task GetStockAsync_WithNotFound_ReturnsNull()
    {
        // Arrange
        InventoryStock? expectedResult = null;
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient, new ProductIdValidator());

        // Act
        var stock = await client.GetStockAsync(999, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, stock);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public async Task GetStockAsync_WithBackendError_ThrowsHttpRequestException(HttpStatusCode status)
    {
        // Arrange
        var expectedStatusCode = status;
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetStockAsync(1, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(action);
        Assert.AreEqual(expectedStatusCode, exception.StatusCode);
    }

    [TestMethod]
    public async Task GetStockAsync_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "{invalid-json")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetStockAsync(1, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<JsonException>(action);
    }

    [TestMethod]
    public async Task GetStockAsync_WithJsonNull_ThrowsInvalidDataException()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "null")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetStockAsync(1, CancellationToken.None);

        // Assert
        await Assert.ThrowsExactlyAsync<InvalidDataException>(action);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public async Task GetStockAsync_WithInvalidId_ThrowsValidationExceptionWithoutSendingHttp(int id)
    {
        // Arrange
        var expectedResult = new { PropertyName = "Id", ErrorMessage = "El id debe ser mayor que cero." };
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetStockAsync(id, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<ValidationException>(action);
        var error = exception.Errors.Single();
        var actualResult = new { error.PropertyName, error.ErrorMessage };
        Assert.AreEqual(expectedResult, actualResult);
        handler.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task GetStockAsync_WithPositiveInput_ReturnsBackendDataWithoutValidation()
    {
        // Arrange
        var expectedResult = new InventoryStock(2, -1);
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(
            HttpStatusCode.OK, """{"productId":2,"availableUnits":-1}""")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient, new ProductIdValidator());

        // Act
        var actualResult = await client.GetStockAsync(1, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, actualResult);
        BackendHttpMock.VerifyGet(handler, "/inventory/products/1");
    }

    [TestMethod]
    public async Task GetStockAsync_WhenCanceled_CancelsTheHttpRequest()
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
        var client = new InventoryClient(httpClient, new ProductIdValidator());

        // Act
        var action = () => client.GetStockAsync(1, cancellation.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(action);
        BackendHttpMock.VerifyGet(handler, "/inventory/products/1");
    }
}
