namespace Bff.Api.Tests.Clients;

using System.Net;
using System.Text.Json;
using Bff.Api.Clients;
using Bff.Api.Tests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Unit")]
public sealed class InventoryClientTests
{
    [TestMethod]
    public async Task GetStockAsync_WithValidResponse_DeserializesAndUsesInventoryRoute()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(
            HttpStatusCode.OK, """{"productId":4,"availableUnits":7}""")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient);

        // Act
        var stock = await client.GetStockAsync(4, CancellationToken.None);

        // Assert
        Assert.IsNotNull(stock);
        Assert.AreEqual(4, stock.ProductId);
        Assert.AreEqual(7, stock.AvailableUnits);
        BackendHttpMock.VerifyGet(handler, "/inventory/products/4");
    }

    [TestMethod]
    public async Task GetStockAsync_WithNotFound_ReturnsNull()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient);

        // Act
        var stock = await client.GetStockAsync(999, CancellationToken.None);

        // Assert
        Assert.IsNull(stock);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public async Task GetStockAsync_WithBackendError_ThrowsHttpRequestException(HttpStatusCode status)
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient);

        // Act
        var action = () => client.GetStockAsync(1, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(action);
        Assert.AreEqual(status, exception.StatusCode);
    }

    [TestMethod]
    public async Task GetStockAsync_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "{invalid-json")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient);

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
        var client = new InventoryClient(httpClient);

        // Act
        var action = () => client.GetStockAsync(1, CancellationToken.None);

        // Assert
        await Assert.ThrowsExactlyAsync<InvalidDataException>(action);
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
        var client = new InventoryClient(httpClient);

        // Act
        var action = () => client.GetStockAsync(1, cancellation.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(action);
        BackendHttpMock.VerifyGet(handler, "/inventory/products/1");
    }
}
