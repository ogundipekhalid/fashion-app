using System.Net;
using System.Text.Json;
using FashionCommerce.Application.Common.Exceptions;
using FashionCommerce.Domain.Exceptions;
using FashionCommerce.Shared.Wrappers;
using ValidationException = FashionCommerce.Application.Common.Exceptions.ValidationException;

namespace FashionCommerce.API.Middlewares;

/// <summary>
/// Catches every unhandled exception and converts it to the standard
/// ApiResponse envelope with the right HTTP status code, so controller
/// actions never need try/catch — they just let exceptions bubble up.
/// Domain exceptions (business rule violations) map to 400/409, never 500;
/// only genuinely unexpected errors become 500, and their details are
/// logged but not leaked to the client.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, response) = exception switch
        {
            ValidationException validationEx => (
                HttpStatusCode.BadRequest,
                ApiResponse.FailureResponse("Validation failed.", validationEx.Errors.SelectMany(e => e.Value))),

            NotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                ApiResponse.FailureResponse(notFoundEx.Message)),

            ForbiddenAccessException forbiddenEx => (
                HttpStatusCode.Forbidden,
                ApiResponse.FailureResponse(forbiddenEx.Message)),

            AuthenticationFailedException authEx => (
                HttpStatusCode.Unauthorized,
                ApiResponse.FailureResponse(authEx.Message)),

            DomainException domainEx => (
                HttpStatusCode.BadRequest,
                ApiResponse.FailureResponse(domainEx.Message)),

            _ => HandleUnexpectedException(exception)
        };

        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }

    private (HttpStatusCode, ApiResponse) HandleUnexpectedException(Exception exception)
    {
        _logger.LogError(exception, "Unhandled exception");
        return (HttpStatusCode.InternalServerError,
            ApiResponse.FailureResponse("An unexpected error occurred. Please try again later."));
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app) =>
        app.UseMiddleware<ExceptionHandlingMiddleware>();
}
