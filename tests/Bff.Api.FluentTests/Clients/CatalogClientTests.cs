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
public sealed class CatalogClientTests
{
    [TestMethod]
    public async Task GetProductAsync_WithValidResponse_DeserializesAndUsesCatalogRoute()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(
            HttpStatusCode.OK,
            """{"id":4,"name":"Teclado","description":"Compacto","price":49.90,"currency":"EUR","supplierCost":25}""")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient);

        // Act
        var product = await client.GetProductAsync(4, CancellationToken.None);

        // Assert
        product.Should().BeEquivalentTo(new CatalogProduct(4, "Teclado", "Compacto", 49.90m, "EUR"));
        BackendHttpMock.VerifyGet(handler, "/catalog/products/4");
    }

    [TestMethod]
    public async Task GetProductAsync_WithNotFound_ReturnsNull()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient);

        // Act
        var product = await client.GetProductAsync(999, CancellationToken.None);

        // Assert
        product.Should().BeNull();
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public async Task GetProductAsync_WithBackendError_ThrowsHttpRequestException(HttpStatusCode status)
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient);

        // Act
        var action = () => client.GetProductAsync(1, CancellationToken.None);

        // Assert
        var assertion = await action.Should().ThrowExactlyAsync<HttpRequestException>();
        assertion.Which.StatusCode.Should().Be(status);
    }

    [TestMethod]
    public async Task GetProductAsync_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "{invalid-json")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient);

        // Act
        var action = () => client.GetProductAsync(1, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<JsonException>();
    }

    [TestMethod]
    public async Task GetProductAsync_WithJsonNull_ThrowsInvalidDataException()
    {
        // Arrange
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(BackendHttpMock.Json(HttpStatusCode.OK, "null")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient);

        // Act
        var action = () => client.GetProductAsync(1, CancellationToken.None);

        // Assert
        await action.Should().ThrowExactlyAsync<InvalidDataException>();
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
        var client = new CatalogClient(httpClient);

        // Act
        var action = () => client.GetProductAsync(1, cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        BackendHttpMock.VerifyGet(handler, "/catalog/products/1");
    }
}
