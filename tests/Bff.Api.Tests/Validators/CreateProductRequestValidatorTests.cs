namespace Bff.Api.Tests.Validators;

using Bff.Api.Requests;
using Bff.Api.Validators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Unit")]
public sealed class CreateProductRequestValidatorTests
{
    [TestMethod]
    [DataRow(false, false, 7, "EUR")]
    [DataRow(true, false, 0, "USD")]
    [DataRow(false, true, int.MaxValue, "EUR")]
    public async Task ValidateAsync_WithValidInput_ReturnsNoErrors(
        bool free, bool maximumLengths, int initialStock, string currency)
    {
        // Arrange
        var expectedResult = new { IsValid = true, InvalidProperties = Array.Empty<string>() };
        var request = new CreateProductRequest(
            maximumLengths ? new string('a', 100) : "Ratón",
            maximumLengths ? new string('a', 1000) : "Ratón inalámbrico.",
            free ? 0m : 35.95m, currency, initialStock);
        var validator = new CreateProductRequestValidator();

        // Act
        var validation = await validator.ValidateAsync(request);
        var actualResult = new
        {
            validation.IsValid,
            InvalidProperties = validation.Errors.Select(error => error.PropertyName).ToArray()
        };

        // Assert
        Assert.AreEqual(expectedResult.IsValid, actualResult.IsValid);
        CollectionAssert.AreEquivalent(expectedResult.InvalidProperties, actualResult.InvalidProperties);
    }

    [TestMethod]
    [DataRow("name-null", nameof(CreateProductRequest.Name))]
    [DataRow("name-empty", nameof(CreateProductRequest.Name))]
    [DataRow("name-whitespace", nameof(CreateProductRequest.Name))]
    [DataRow("name-too-long", nameof(CreateProductRequest.Name))]
    [DataRow("description-null", nameof(CreateProductRequest.Description))]
    [DataRow("description-empty", nameof(CreateProductRequest.Description))]
    [DataRow("description-whitespace", nameof(CreateProductRequest.Description))]
    [DataRow("description-too-long", nameof(CreateProductRequest.Description))]
    [DataRow("price-null", nameof(CreateProductRequest.Price))]
    [DataRow("price-negative", nameof(CreateProductRequest.Price))]
    [DataRow("currency-null", nameof(CreateProductRequest.Currency))]
    [DataRow("currency-empty", nameof(CreateProductRequest.Currency))]
    [DataRow("currency-whitespace", nameof(CreateProductRequest.Currency))]
    [DataRow("currency-lowercase", nameof(CreateProductRequest.Currency))]
    [DataRow("currency-short", nameof(CreateProductRequest.Currency))]
    [DataRow("currency-long", nameof(CreateProductRequest.Currency))]
    [DataRow("currency-digits", nameof(CreateProductRequest.Currency))]
    [DataRow("currency-newline", nameof(CreateProductRequest.Currency))]
    [DataRow("stock-negative", nameof(CreateProductRequest.InitialStock))]
    public async Task ValidateAsync_WithInvalidInput_ReportsTheProperty(string invalidCase, string propertyName)
    {
        // Arrange
        var expectedResult = new { IsValid = false, InvalidProperties = new[] { propertyName } };
        var validRequest = new CreateProductRequest("Ratón", "Ratón inalámbrico.", 35.95m, "EUR", 7);
        var request = invalidCase switch
        {
            "name-null" => validRequest with { Name = null },
            "name-empty" => validRequest with { Name = "" },
            "name-whitespace" => validRequest with { Name = "   " },
            "name-too-long" => validRequest with { Name = new string('a', 101) },
            "description-null" => validRequest with { Description = null },
            "description-empty" => validRequest with { Description = "" },
            "description-whitespace" => validRequest with { Description = "   " },
            "description-too-long" => validRequest with { Description = new string('a', 1001) },
            "price-null" => validRequest with { Price = null },
            "price-negative" => validRequest with { Price = -0.01m },
            "currency-null" => validRequest with { Currency = null },
            "currency-empty" => validRequest with { Currency = "" },
            "currency-whitespace" => validRequest with { Currency = "   " },
            "currency-lowercase" => validRequest with { Currency = "eur" },
            "currency-short" => validRequest with { Currency = "EU" },
            "currency-long" => validRequest with { Currency = "EURO" },
            "currency-digits" => validRequest with { Currency = "123" },
            "currency-newline" => validRequest with { Currency = "EUR\n" },
            "stock-negative" => validRequest with { InitialStock = -1 },
            _ => throw new ArgumentOutOfRangeException(nameof(invalidCase))
        };
        var validator = new CreateProductRequestValidator();

        // Act
        var validation = await validator.ValidateAsync(request);
        var actualResult = new
        {
            validation.IsValid,
            InvalidProperties = validation.Errors.Select(error => error.PropertyName).ToArray()
        };

        // Assert
        Assert.AreEqual(expectedResult.IsValid, actualResult.IsValid);
        CollectionAssert.AreEquivalent(expectedResult.InvalidProperties, actualResult.InvalidProperties);
    }

    [TestMethod]
    public async Task ValidateAsync_WithSeveralInvalidFields_ReportsAllErrors()
    {
        // Arrange
        var expectedResult = new
        {
            IsValid = false,
            InvalidProperties = new[]
            {
                nameof(CreateProductRequest.Name), nameof(CreateProductRequest.Description),
                nameof(CreateProductRequest.Price), nameof(CreateProductRequest.Currency),
                nameof(CreateProductRequest.InitialStock)
            }
        };
        var request = new CreateProductRequest("", "", null, "", -1);
        var validator = new CreateProductRequestValidator();

        // Act
        var validation = await validator.ValidateAsync(request);
        var actualResult = new
        {
            validation.IsValid,
            InvalidProperties = validation.Errors.Select(error => error.PropertyName).ToArray()
        };

        // Assert
        Assert.AreEqual(expectedResult.IsValid, actualResult.IsValid);
        CollectionAssert.AreEquivalent(expectedResult.InvalidProperties, actualResult.InvalidProperties);
    }

    [TestMethod]
    public async Task ValidateAsync_WithNullRequest_ReturnsRequiredRequestError()
    {
        // Arrange
        var expectedResult = new
        {
            IsValid = false, PropertyName = "Request",
            ErrorMessage = "Los datos del producto son obligatorios."
        };
        var validator = new CreateProductRequestValidator();

        // Act
        var validation = await validator.ValidateAsync((CreateProductRequest)null!);
        var error = validation.Errors.Single();
        var actualResult = new { validation.IsValid, error.PropertyName, error.ErrorMessage };

        // Assert
        Assert.AreEqual(expectedResult, actualResult);
    }
}

