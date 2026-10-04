namespace Bff.Api.Tests.ExceptionHandlers;

using Bff.Api.ExceptionHandlers;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Unit")]
public sealed class BackendExceptionHandlerTests
{

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task TryHandleAsync_WithValidationErrors_GroupsMessagesAndSelectsTitle(bool onlyIdErrors)
    {
        // Arrange
        var expectedResult = new
        {
            Handled = true,
            StatusCode = StatusCodes.Status400BadRequest,
            ProblemStatus = (int?)StatusCodes.Status400BadRequest,
            Title = onlyIdErrors ? "El id debe ser mayor que cero." : "Los datos de entrada no son válidos."
        };
        var expectedErrors = onlyIdErrors
            ? new Dictionary<string, string[]> { ["Id"] = ["Primer error.", "Segundo error."] }
            : new Dictionary<string, string[]>
            {
                ["Name"] = ["Primer error.", "Segundo error."],
                ["Price"] = ["El precio no puede ser negativo."]
            };
        var failures = expectedErrors.SelectMany(property =>
            property.Value.Select(message => new ValidationFailure(property.Key, message))).ToArray();
        var exception = new ValidationException(failures);
        var context = new DefaultHttpContext();
        ProblemDetails? writtenProblem = null;
        var problemDetails = new Mock<IProblemDetailsService>(MockBehavior.Strict);
        problemDetails.Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(output => writtenProblem = output.ProblemDetails)
            .Returns(ValueTask.FromResult(true));
        var handler = new BackendExceptionHandler(problemDetails.Object,
            NullLogger<BackendExceptionHandler>.Instance);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        var validationProblem = (HttpValidationProblemDetails)writtenProblem!;
        var actualResult = new
        {
            Handled = handled,
            StatusCode = context.Response.StatusCode,
            ProblemStatus = validationProblem.Status,
            Title = validationProblem.Title
        };
        var actualErrors = validationProblem.Errors;

        // Assert
        Assert.AreEqual<object>(expectedResult, actualResult);
        CollectionAssert.AreEquivalent(expectedErrors.Keys.ToArray(), actualErrors.Keys.ToArray());
        foreach (var property in expectedErrors)
            CollectionAssert.AreEqual(property.Value, actualErrors[property.Key]);
        problemDetails.Verify(service => service.TryWriteAsync(
            It.Is<ProblemDetailsContext>(output => output.HttpContext == context)), Times.Once);
        problemDetails.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TryHandleAsync_WhenProblemCannotBeWritten_ReturnsFalse()
    {
        // Arrange
        var expectedResult = new
        {
            Handled = false,
            StatusCode = StatusCodes.Status500InternalServerError,
            ProblemStatus = (int?)StatusCodes.Status500InternalServerError,
            Title = "Ocurrió un error inesperado."
        };
        var context = new DefaultHttpContext();
        var exception = new InvalidOperationException("Fallo simulado.");
        ProblemDetails? writtenProblem = null;
        var problemDetails = new Mock<IProblemDetailsService>(MockBehavior.Strict);
        problemDetails.Setup(service => service.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(output => writtenProblem = output.ProblemDetails)
            .Returns(ValueTask.FromResult(false));
        var handler = new BackendExceptionHandler(problemDetails.Object,
            NullLogger<BackendExceptionHandler>.Instance);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        var actualResult = new
        {
            Handled = handled,
            StatusCode = context.Response.StatusCode,
            ProblemStatus = writtenProblem!.Status,
            Title = writtenProblem.Title
        };

        // Assert
        Assert.AreEqual<object>(expectedResult, actualResult);
        problemDetails.Verify(service => service.TryWriteAsync(
            It.Is<ProblemDetailsContext>(output => output.HttpContext == context)), Times.Once);
        problemDetails.VerifyNoOtherCalls();
    }

    [TestMethod]
    public async Task TryHandleAsync_WhenRequestIsAborted_DoesNotWriteProblemDetails()
    {
        // Arrange
        const bool expectedResult = false;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = cancellation.Token };
        var problemDetails = new Mock<IProblemDetailsService>(MockBehavior.Strict);
        var handler = new BackendExceptionHandler(problemDetails.Object,
            NullLogger<BackendExceptionHandler>.Instance);
        var exception = new OperationCanceledException(cancellation.Token);

        // Act
        var handled = await handler.TryHandleAsync(context, exception, cancellation.Token);

        // Assert
        Assert.AreEqual(expectedResult, handled);
        problemDetails.VerifyNoOtherCalls();
    }
}
