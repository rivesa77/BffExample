namespace Bff.Api.Tests.Configuration;

using Bff.Api.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[TestCategory("Unit")]
public sealed class BackendOptionsTests
{
    [TestMethod]
    [DataRow("http://localhost:5101/")]
    [DataRow("https://api.example.com/catalog/")]
    public void IsValidUrl_WithAbsoluteHttpUrlAndTrailingSlash_ReturnsTrue(string url)
    {
        // Arrange
        var baseUrl = url;

        // Act
        var isValid = BackendOptions.IsValidUrl(baseUrl);

        // Assert
        Assert.IsTrue(isValid);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("catalog/")]
    [DataRow("http://localhost:5101")]
    [DataRow("https://api.example.com/catalog")]
    [DataRow("ftp://api.example.com/")]
    public void IsValidUrl_WithInvalidBaseUrl_ReturnsFalse(string url)
    {
        // Arrange
        var baseUrl = url;

        // Act
        var isValid = BackendOptions.IsValidUrl(baseUrl);

        // Assert
        Assert.IsFalse(isValid);
    }
}
