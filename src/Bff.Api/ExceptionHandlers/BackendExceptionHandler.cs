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

        ProblemDetails problem = exception is ValidationException validation
            ? new HttpValidationProblemDetails(validation.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray()))
            : new ProblemDetails();
        problem.Status = statusCode;
        problem.Title = statusCode switch
        {
            400 when exception is ValidationException inputError
                && inputError.Errors.All(error => error.PropertyName == "Id") => "El id debe ser mayor que cero.",
            400 => "Los datos de entrada no son válidos.",
            504 => "El servicio de datos tardó demasiado en responder.",
            502 => "No se pudo obtener la información del producto.",
            _ => "Ocurrió un error inesperado."
        };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem
        });
    }
}
