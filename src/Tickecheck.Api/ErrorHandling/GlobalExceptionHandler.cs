using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Tickecheck.Application.Common;
using Tickecheck.Domain.Common;

namespace Tickecheck.Api.ErrorHandling;

/// <summary>
/// The single place where exceptions become HTTP responses. Every error leaves the
/// API as a ProblemDetails JSON (RFC 9457), so clients handle one format only.
/// </summary>
public class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error")
        };

        var problem = new ProblemDetails { Status = status, Title = title };

        if (exception is ValidationException validationException)
        {
            problem.Detail = "One or more fields are invalid.";
            problem.Extensions["errors"] = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
        }
        else if (status == StatusCodes.Status500InternalServerError)
        {
            // Unexpected errors are logged in full but never sent to the client:
            // their message could expose internal details.
            logger.LogError(exception, "Unhandled exception");
            problem.Detail = "An unexpected error occurred.";
        }
        else
        {
            problem.Detail = exception.Message;
        }

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
