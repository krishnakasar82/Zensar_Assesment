using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OT.Assessment.Application;

namespace OT.Assessment.Api.Middleware;

internal sealed class ApiExceptionHandler(
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is BadHttpRequestException)
        {
            logger.LogWarning(exception, "The client sent an invalid HTTP request.");
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Bad Request",
                Detail = "The request body is missing or malformed.",
                Instance = httpContext.Request.Path
            }, cancellationToken);

            return true;
        }

        if (exception is not BrokerUnavailableException)
        {
            return false;
        }

        logger.LogError(exception, "The wager message broker is unavailable.");
        httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status503ServiceUnavailable,
            Title = "Wager message broker unavailable",
            Detail = "The wager could not be accepted because the message broker is unavailable.",
            Instance = httpContext.Request.Path
        }, cancellationToken);

        return true;
    }
}
