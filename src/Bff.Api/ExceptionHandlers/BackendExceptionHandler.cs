namespace Bff.Api.ExceptionHandlers;

using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public sealed class BackendExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<BackendExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (context.RequestAborted.IsCancellationRequested)
            return false;

        var statusCode = exception switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            OperationCanceledException => StatusCodes.Status504GatewayTimeout,
            HttpRequestException or JsonException or InvalidDataException => StatusCodes.Status502BadGateway,
            _ => StatusCodes.Status500InternalServerError
        };

        logger.LogError(exception, "Error al atender {Path}", context.Request.Path);
        context.Response.StatusCode = statusCode;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = statusCode switch
                {
                    400 => "El id debe ser mayor que cero.",
                    504 => "El servicio de datos tardó demasiado en responder.",
                    502 => "No se pudo obtener la información del producto.",
                    _ => "Ocurrió un error inesperado."
                }
            }
        });
    }
}
