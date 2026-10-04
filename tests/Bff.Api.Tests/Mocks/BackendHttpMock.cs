namespace Bff.Api.Tests.Mocks;

using System.Net;
using System.Text;
using Moq;
using Moq.Protected;

// El transporte HTTP se sustituye por Moq: nunca se abre una conexión al backend.
public static class BackendHttpMock
{
    public static Mock<HttpMessageHandler> Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        // HttpClient y la fábrica liberan el handler al finalizar cada prueba.
        handler.Protected().Setup("Dispose", ItExpr.IsAny<bool>());
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .Returns(respond);
        return handler;
    }

    public static HttpClient CreateClient(Mock<HttpMessageHandler> handler) =>
        new(handler.Object) { BaseAddress = new Uri("http://backend.test/") };

    public static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    public static void VerifyGet(Mock<HttpMessageHandler> handler, string path) =>
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request =>
                request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == path),
            ItExpr.IsAny<CancellationToken>());

    public static void VerifyPost(Mock<HttpMessageHandler> handler, string path) =>
        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(request =>
                request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == path),
            ItExpr.IsAny<CancellationToken>());
}
