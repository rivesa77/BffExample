namespace Bff.Api.FluentTests.ExceptionHandlers;

using Bff.Api.ExceptionHandlers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

[TestClass]
[TestCategory("Unit")]
public sealed class BackendExceptionHandlerTests
{
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
        handled.Should().Be(expectedResult);
        problemDetails.VerifyNoOtherCalls();
    }
}
