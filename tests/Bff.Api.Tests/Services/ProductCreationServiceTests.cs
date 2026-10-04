namespace Bff.Api.Tests.Services;

using Bff.Api.Interfaces;
using Bff.Api.Models;
using Bff.Api.Requests;
using Bff.Api.Services;
using Bff.Api.Validators;
using FluentValidation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Unit")]
public sealed class ProductCreationServiceTests
{
    [TestMethod]
    public async Task CreateAsync_WithValidInput_DelegatesAndReturnsPublicDto()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var expectedResult = new CreatedProductDto(3, "Ratón", "Ratón inalámbrico.", 35.95m, "EUR");
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        catalog.Setup(client => client.CreateProductAsync(request, cancellation.Token))
            .ReturnsAsync(new CatalogProduct(3, "Ratón", "Ratón inalámbrico.", 35.95m, "EUR"));
        var service = new ProductCreationService(catalog.Object, new CreateProductRequestValidator());

        // Act
        var actualResult = await service.CreateAsync(request, cancellation.Token);

        // Assert
        Assert.AreEqual(expectedResult, actualResult);
        catalog.Verify(client => client.CreateProductAsync(request, cancellation.Token), Times.Once);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow("name", nameof(CreateProductRequest.Name))]
    [DataRow("description", nameof(CreateProductRequest.Description))]
    [DataRow("price", nameof(CreateProductRequest.Price))]
    [DataRow("currency", nameof(CreateProductRequest.Currency))]
    [DataRow("stock", nameof(CreateProductRequest.InitialStock))]
    [DataRow("null-request", "Request")]
    public async Task CreateAsync_WithInvalidInput_ThrowsWithoutCallingCatalog(string invalidCase, string propertyName)
    {
        // Arrange
        string[] expectedResult = [propertyName];
        var validRequest = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var request = invalidCase switch
        {
            "name" => validRequest with { Name = "" },
            "description" => validRequest with { Description = "" },
            "price" => validRequest with { Price = null },
            "currency" => validRequest with { Currency = "eur" },
            "stock" => validRequest with { InitialStock = -1 },
            "null-request" => null!,
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase))
        };
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        var service = new ProductCreationService(catalog.Object, new CreateProductRequestValidator());

        // Act
        var action = () => service.CreateAsync(request, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<ValidationException>(action);
        var actualResult = exception.Errors.Select(error => error.PropertyName).ToArray();
        CollectionAssert.AreEquivalent(expectedResult, actualResult);
        catalog.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task CreateAsync_WhenCatalogFails_PropagatesTheFailure()
    {
        // Arrange
        var expectedException = new HttpRequestException("Fallo simulado.");
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        catalog.Setup(client => client.CreateProductAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);
        var service = new ProductCreationService(catalog.Object, new CreateProductRequestValidator());

        // Act
        var action = () => service.CreateAsync(request, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(action);
        Assert.AreSame(expectedException, exception);
        catalog.VerifyAll();
    }

    [TestMethod]
    public async Task CreateAsync_WithBackendValues_MapsOutputWithoutValidation()
    {
        // Arrange
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var expectedResult = new CreatedProductDto(3, "", "", -1m, "eur");
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        catalog.Setup(client => client.CreateProductAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogProduct(3, "", "", -1m, "eur"));
        var service = new ProductCreationService(catalog.Object, new CreateProductRequestValidator());

        // Act
        var actualResult = await service.CreateAsync(request, CancellationToken.None);

        // Assert
        Assert.AreEqual(expectedResult, actualResult);
        catalog.VerifyAll();
    }

    [TestMethod]
    public async Task CreateAsync_WhenCanceled_ForwardsCancellationToCatalog()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var catalog = new Mock<ICatalogClient>(MockBehavior.Strict);
        catalog.Setup(client => client.CreateProductAsync(request, cancellation.Token))
            .Returns((CreateProductRequest _, CancellationToken token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<CatalogProduct>(token);
            });
        var service = new ProductCreationService(catalog.Object, new CreateProductRequestValidator());

        // Act
        var action = () => service.CreateAsync(request, cancellation.Token);

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(action);
        catalog.Verify(client => client.CreateProductAsync(request, cancellation.Token), Times.Once);
        catalog.VerifyNoOtherCalls();
    }
}

