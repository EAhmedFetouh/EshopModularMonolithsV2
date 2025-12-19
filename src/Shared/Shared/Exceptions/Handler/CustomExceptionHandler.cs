
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Shared.Exceptions.Handler;

public class CustomExceptionHandler 
    (ILogger<CustomExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
    HttpContext context,
    Exception exception,
    CancellationToken cancellationToken)
    {
        // 1️⃣ Log the exception
        logger.LogError(
            exception,
            "Error Message: {exceptionMessage}, Time of occurrence: {time}",
            exception.Message,
            DateTime.UtcNow
        );

        // 2️⃣ Map exception to details using switch expression
        (string Detail, string Title, int StatusCode) details = exception switch
        {
            InternalServerException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status500InternalServerError
            ),

            ValidationException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),

            BadRequestException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),

            NotFoundException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status404NotFound
            ),

            _ =>
            (
                "An unexpected error occurred.",
                "InternalServerError",
                context.Response.StatusCode = StatusCodes.Status500InternalServerError
            )
        };

        // 3️⃣ Create ProblemDetails response
        var problemDetails = new ProblemDetails
        {
            Title = details.Title,
            Detail = details.Detail,
            Status = details.StatusCode,
            Instance = context.Request.Path
        };

        // 4️⃣ Add TraceId for debugging
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        if (exception is ValidationException ve)
        {
            var errors = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.ErrorMessage).Distinct().ToArray()
                );

            problemDetails.Detail = "Validation failed. See validationErrors.";
            problemDetails.Extensions["validationErrors"] = errors;
        }


        // 5️⃣ Write response
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true; // Exception handled successfully
    }

}
