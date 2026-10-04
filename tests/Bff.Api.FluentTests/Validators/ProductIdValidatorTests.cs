namespace Bff.Api.FluentTests.Validators;

using Bff.Api.Validators;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Unit")]
public sealed class ProductIdValidatorTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(4)]
    [DataRow(int.MaxValue)]
    public async Task ValidateAsync_WithPositiveId_ReturnsNoErrors(int id)
    {
        // Arrange
        var expectedResult = new { IsValid = true, InvalidProperties = Array.Empty<string>() };
        var validator = new ProductIdValidator();

        // Act
        var validation = await validator.ValidateAsync(id);
        var actualResult = new
        {
            validation.IsValid,
            InvalidProperties = validation.Errors.Select(error => error.PropertyName).ToArray()
        };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(int.MinValue)]
    public async Task ValidateAsync_WithNonPositiveId_ReturnsInputError(int id)
    {
        // Arrange
        var expectedResult = new
        {
            IsValid = false,
            PropertyName = "Id",
            ErrorMessage = "El id debe ser mayor que cero."
        };
        var validator = new ProductIdValidator();

        // Act
        var validation = await validator.ValidateAsync(id);
        var error = validation.Errors.Single();
        var actualResult = new { validation.IsValid, error.PropertyName, error.ErrorMessage };

        // Assert
        actualResult.Should().BeEquivalentTo(expectedResult);
    }
}

