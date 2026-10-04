namespace Bff.Api.FluentTests.Clients;

using System.Net;
using System.Text.Json;
using Bff.Api.Clients;
using Bff.Api.Models;
using Bff.Api.FluentTests.Mocks;
using FluentAssertions;
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
        var client = new InventoryClient(httpClient);

        // Act
        var stock = await client.GetStockAsync(4, CancellationToken.None);

        // Assert
        stock.Should().BeEquivalentTo(expectedResult);
        BackendHttpMock.VerifyGet(handler, "/inventory/products/4");
    }

    [TestMethod]
    public async Task GetStockAsync_WithNotFound_ReturnsNull()
    {
        // Arrange
        InventoryStock? expectedResult = null;
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new InventoryClient(httpClient);

        // Act
        var stock = await client.GetStockAsync(999, CancellationToken.None);

        // Assert
        stock.Should().BeEquivalentTo(expectedResult);
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
        var client = new InventoryClient(httpClient);

        // Act
        var action = () => client.GetStockAsync(1, CancellationToken.None);

        // Assert
        var assertion = await action.Should().ThrowExactlyAsync<HttpRequestException>();
        assertion.Which.StatusCode.Should().Be(expectedStatusCode);
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
        await action.Should().ThrowAsync<JsonException>();
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
        await action.Should().ThrowExactlyAsync<InvalidDataException>();
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
        await action.Should().ThrowAsync<OperationCanceledException>();
        BackendHttpMock.VerifyGet(handler, "/inventory/products/1");
    }
}
