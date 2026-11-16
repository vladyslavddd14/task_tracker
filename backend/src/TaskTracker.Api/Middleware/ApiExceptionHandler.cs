using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TaskTracker.Application.Common;
using TaskTracker.Domain;

namespace TaskTracker.Api.Middleware;

/// <summary>Turns exceptions into RFC 7807 problem details. Stack traces never reach the client.</summary>
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var problem = exception switch
        {
            ValidationException validation => CreateValidationProblem(validation),
            DomainException domain => Create(StatusCodes.Status400BadRequest, "Validation failed", domain.Message),
            NotFoundException notFound => Create(StatusCodes.Status404NotFound, "Not found", notFound.Message),
            ConflictException conflict => Create(StatusCodes.Status409Conflict, "Conflict", conflict.Message),
            UnauthorizedException unauthorized => Create(StatusCodes.Status401Unauthorized, "Unauthorized",
                unauthorized.Message),
            _ => Create(StatusCodes.Status500InternalServerError, "Internal server error",
                "An unexpected error occurred.")
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }

        problem.Instance = context.Request.Path;
        context.Response.StatusCode = problem.Status!.Value;
        // Pass the runtime type, otherwise ValidationProblemDetails would lose its "errors" dictionary.
        await context.Response.WriteAsJsonAsync(problem, problem.GetType(), (System.Text.Json.JsonSerializerOptions?)null,
            "application/problem+json", ct);
        return true;
    }

    private static ProblemDetails Create(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred."
        };
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
