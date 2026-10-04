namespace Bff.Api.FluentTests.Services;

using Bff.Api.Interfaces;
using Bff.Api.Models;
using Bff.Api.Services;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Unit")]
public sealed class ProductPageServiceTests
{
    [TestMethod]
    [DataRow(7, true, "Disponible")]
    [DataRow(0, false, "Agotado")]
    [DataRow(-1, false, "Agotado")]
    public async Task GetAsync_WithStock_CombinesProductAndAvailability(
        int availableUnits, bool canBuy, string availability)
    {
        // Arrange
        var product = new CatalogProduct(1, "Portátil", "Equipo para trabajar.", 899.90m, "EUR");
        var expectedResult = new ProductPageDto(
            1, "Portátil", "Equipo para trabajar.", 899.90m,
            "899,90 EUR", availability, canBuy);
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryClient>(MockBehavior.Strict);
        catalog.Setup(client => client.GetProductAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        inventory.Setup(client => client.GetStockAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InventoryStock(1, availableUnits));
        var service = new ProductPageService(catalog.Object, inventory.Object);

        // Act
        var result = await service.GetAsync(1, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
        catalog.VerifyAll();
        inventory.VerifyAll();
    }

    [TestMethod]
    public async Task GetAsync_WhenProductDoesNotExist_ReturnsNull()
    {
        // Arrange
        ProductPageDto? expectedResult = null;
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryClient>(MockBehavior.Strict);
        catalog.Setup(client => client.GetProductAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CatalogProduct?)null);
        inventory.Setup(client => client.GetStockAsync(999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryStock?)null);
        var service = new ProductPageService(catalog.Object, inventory.Object);

        // Act
        var result = await service.GetAsync(999, CancellationToken.None);

        // Assert
        result.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    public async Task GetAsync_WhenInventoryIsMissing_ThrowsInvalidDataException()
    {
        // Arrange
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryClient>(MockBehavior.Strict);
        catalog.Setup(client => client.GetProductAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProduct(1, "Producto", "Descripción", 10m, "EUR"));
        inventory.Setup(client => client.GetStockAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InventoryStock?)null);
        var service = new ProductPageService(catalog.Object, inventory.Object);

        // Act
        var action = () => service.GetAsync(1, CancellationToken.None);

        // Assert
        await action.Should().ThrowExactlyAsync<InvalidDataException>();
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task GetAsync_WhenEitherBackendFails_PropagatesTheFailure(bool catalogFails)
    {
        // Arrange
        var expectedException = new HttpRequestException("Fallo simulado.");
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryClient>(MockBehavior.Strict);
        catalog.Setup(client => client.GetProductAsync(1, It.IsAny<CancellationToken>()))
            .Returns(catalogFails
                ? Task.FromException<CatalogProduct?>(expectedException)
                : Task.FromResult<CatalogProduct?>(new(1, "Producto", "Descripción", 10m, "EUR")));
        inventory.Setup(client => client.GetStockAsync(1, It.IsAny<CancellationToken>()))
            .Returns(catalogFails
                ? Task.FromResult<InventoryStock?>(new(1, 3))
                : Task.FromException<InventoryStock?>(expectedException));
        var service = new ProductPageService(catalog.Object, inventory.Object);

        // Act
        var action = () => service.GetAsync(1, CancellationToken.None);

        // Assert
        var assertion = await action.Should().ThrowExactlyAsync<HttpRequestException>();
        assertion.Which.Should().BeSameAs(expectedException);
    }

    [TestMethod]
    public async Task GetAsync_StartsBothRequestsBeforeEitherCompletes()
    {
        // Arrange
        var expectedResult = new ProductPageDto(1, "Producto", "Descripción", 10m, "10,00 EUR", "Disponible", true);
        var productCompletion = new TaskCompletionSource<CatalogProduct?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stockCompletion = new TaskCompletionSource<InventoryStock?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryClient>(MockBehavior.Strict);
        catalog.Setup(client => client.GetProductAsync(1, It.IsAny<CancellationToken>())).Returns(productCompletion.Task);
        inventory.Setup(client => client.GetStockAsync(1, It.IsAny<CancellationToken>())).Returns(stockCompletion.Task);
        var service = new ProductPageService(catalog.Object, inventory.Object);

        // Act
        var resultTask = service.GetAsync(1, CancellationToken.None);
        var startedBothRequests = catalog.Invocations.Count == 1 && inventory.Invocations.Count == 1;
        var wasWaitingForResponses = !resultTask.IsCompleted;
        productCompletion.SetResult(new(1, "Producto", "Descripción", 10m, "EUR"));
        stockCompletion.SetResult(new(1, 2));
        var result = await resultTask;

        // Assert
        startedBothRequests.Should().BeTrue("Ambos backends deben consultarse antes de esperar sus respuestas.");
        wasWaitingForResponses.Should().BeTrue();
        result.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    public async Task GetAsync_WithCancellation_ForwardsTokenToBothClients()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        var inventory = new Mock<IInventoryClient>(MockBehavior.Strict);
        catalog.Setup(client => client.GetProductAsync(1, cancellation.Token))
            .Returns((int _, CancellationToken token) => Task.FromCanceled<CatalogProduct?>(token));
        inventory.Setup(client => client.GetStockAsync(1, cancellation.Token))
            .Returns((int _, CancellationToken token) => Task.FromCanceled<InventoryStock?>(token));
        var service = new ProductPageService(catalog.Object, inventory.Object);
        await cancellation.CancelAsync();

        // Act
        var action = () => service.GetAsync(1, cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        catalog.Verify(client => client.GetProductAsync(1, cancellation.Token), Times.Once);
        inventory.Verify(client => client.GetStockAsync(1, cancellation.Token), Times.Once);
    }
}
