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

        var problem = CreateProblemDetails(exception);

        logger.LogError(exception, "Error al atender {Path}", context.Request.Path);
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem
        });
    }

    private static ProblemDetails CreateProblemDetails(Exception exception)
    {
        if (exception is ValidationException validation)
            return CreateValidationProblemDetails(validation);

        var (statusCode, title) = exception switch
        {
            OperationCanceledException =>
                (StatusCodes.Status504GatewayTimeout, "El servicio de datos tardó demasiado en responder."),
            HttpRequestException or JsonException or InvalidDataException =>
                (StatusCodes.Status502BadGateway, "No se pudo obtener la información del producto."),
            _ =>
                (StatusCodes.Status500InternalServerError, "Ocurrió un error inesperado.")
        };

        return new ProblemDetails { Status = statusCode, Title = title };
    }

    private static HttpValidationProblemDetails CreateValidationProblemDetails(ValidationException exception)
    {
        var errorsByProperty = exception.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        var onlyIdErrors = errorsByProperty.Keys.All(propertyName => propertyName == "Id");

        return new HttpValidationProblemDetails(errorsByProperty)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = onlyIdErrors
                ? "El id debe ser mayor que cero."
                : "Los datos de entrada no son válidos."
        };
    }
}
