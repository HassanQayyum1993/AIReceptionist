using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace AIReceptionist.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log, IHostEnvironment env)
    {
        _next = next;
        _log = log;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // Log with path & method
            var method = context.Request?.Method ?? "?";
            var path = context.Request?.Path.Value ?? "?";
            _log.LogError(ex, "Unhandled exception processing request {Method} {Path}", method, path);

            // Map exception types to HTTP status codes and problem titles
            int statusCode;
            string title;

            if (ex is ArgumentException)
            {
                statusCode = StatusCodes.Status400BadRequest;
                title = "Invalid request";
            }
            else if (ex is HttpRequestException)
            {
                statusCode = StatusCodes.Status503ServiceUnavailable;
                title = "Downstream service error";
            }
            else if (ex is InvalidOperationException)
            {
                statusCode = StatusCodes.Status502BadGateway;
                title = "Upstream processing error";
            }
            else
            {
                statusCode = StatusCodes.Status500InternalServerError;
                title = "An unexpected error occurred";
            }

            var problem = new ProblemDetails
            {
                Title = title,
                Status = statusCode,
                Detail = _env.IsDevelopment() ? ex.Message : "An error occurred while processing your request."
            };

            if (_env.IsDevelopment())
            {
                problem.Extensions["exception"] = ex.GetType().FullName;
                problem.Extensions["stackTrace"] = ex.StackTrace;
            }

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.Serialize(problem, options);
            await context.Response.WriteAsync(json);
        }
    }
}
