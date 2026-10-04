namespace Bff.Api.FluentTests.Clients;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bff.Api.Clients;
using Bff.Api.Models;
using Bff.Api.Requests;
using Bff.Api.FluentTests.Mocks;
using Bff.Api.Validators;
using FluentValidation;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Unit")]
public sealed class CatalogClientCreationTests
{
    [TestMethod]
    public async Task CreateProductAsync_WithValidInput_PostsJsonAndDeserializesProduct()
    {
        // Arrange
        var expectedRequest = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var expectedResult = new CatalogProduct(3, "Ratón", "Ratón inalámbrico.", 35.95m, "EUR");
        const string expectedContentType = "application/json";
        CreateProductRequest? sentRequest = null;
        string? sentContentType = null;
        var handler = BackendHttpMock.Create(async (request, token) =>
        {
            sentRequest = await request.Content!.ReadFromJsonAsync<CreateProductRequest>(token);
            sentContentType = request.Content!.Headers.ContentType?.MediaType;
            return BackendHttpMock.Json(HttpStatusCode.Created,
                """{"id":3,"name":"Ratón","description":"Ratón inalámbrico.","price":35.95,"currency":"EUR","supplierCost":10}""");
        });
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator(), new CreateProductRequestValidator());

        // Act
        var actualResult = await client.CreateProductAsync(expectedRequest, CancellationToken.None);

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
        sentRequest.Should().BeEquivalentTo(expectedRequest);
        sentContentType.Should().Be(expectedContentType);
        BackendHttpMock.VerifyPost(handler, "/catalog/products");
    }

    [TestMethod]
    [DataRow("name", nameof(CreateProductRequest.Name))]
    [DataRow("price", nameof(CreateProductRequest.Price))]
    [DataRow("stock", nameof(CreateProductRequest.InitialStock))]
    [DataRow("null-request", "Request")]
    public async Task CreateProductAsync_WithInvalidInput_ThrowsWithoutSendingHttp(string invalidCase, string propertyName)
    {
        // Arrange
        string[] expectedResult = [propertyName];
        var validRequest = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var request = invalidCase switch
        {
            "name" => validRequest with { Name = "" },
            "price" => validRequest with { Price = null },
            "stock" => validRequest with { InitialStock = -1 },
            "null-request" => null!,
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase))
        };
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator(), new CreateProductRequestValidator());

        // Act
        var action = () => client.CreateProductAsync(request, CancellationToken.None);

        // Assert
        var assertion = await action.Should().ThrowExactlyAsync<ValidationException>();
        var actualResult = assertion.Which.Errors.Select(error => error.PropertyName).ToArray();
        actualResult.Should().BeEquivalentTo(expectedResult);
        handler.VerifyNoOtherCalls();
    }

    [TestMethod]
    [DataRow(HttpStatusCode.InternalServerError)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public async Task CreateProductAsync_WithBackendError_ThrowsHttpRequestException(HttpStatusCode status)
    {
        // Arrange
        var expectedStatusCode = status;
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator(), new CreateProductRequestValidator());

        // Act
        var action = () => client.CreateProductAsync(request, CancellationToken.None);

        // Assert
        var assertion = await action.Should().ThrowExactlyAsync<HttpRequestException>();
        assertion.Which.StatusCode.Should().Be(expectedStatusCode);
        BackendHttpMock.VerifyPost(handler, "/catalog/products");
    }

    [TestMethod]
    public async Task CreateProductAsync_WithMalformedJson_ThrowsJsonException()
    {
        // Arrange
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(
            BackendHttpMock.Json(HttpStatusCode.Created, "{invalid-json")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator(), new CreateProductRequestValidator());

        // Act
        var action = () => client.CreateProductAsync(request, CancellationToken.None);

        // Assert
        await action.Should().ThrowAsync<JsonException>();
        BackendHttpMock.VerifyPost(handler, "/catalog/products");
    }

    [TestMethod]
    public async Task CreateProductAsync_WithNullResponse_ThrowsInvalidDataException()
    {
        // Arrange
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var handler = BackendHttpMock.Create((_, _) => Task.FromResult(
            BackendHttpMock.Json(HttpStatusCode.Created, "null")));
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator(), new CreateProductRequestValidator());

        // Act
        var action = () => client.CreateProductAsync(request, CancellationToken.None);

        // Assert
        await action.Should().ThrowExactlyAsync<InvalidDataException>();
        BackendHttpMock.VerifyPost(handler, "/catalog/products");
    }

    [TestMethod]
    public async Task CreateProductAsync_WhenCanceled_CancelsTheHttpRequest()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource();
        var request = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var handler = BackendHttpMock.Create(async (_, token) =>
        {
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var httpClient = BackendHttpMock.CreateClient(handler);
        var client = new CatalogClient(httpClient, new ProductIdValidator(), new CreateProductRequestValidator());

        // Act
        var action = () => client.CreateProductAsync(request, cancellation.Token);

        // Assert
        await action.Should().ThrowAsync<OperationCanceledException>();
        BackendHttpMock.VerifyPost(handler, "/catalog/products");
    }
}

