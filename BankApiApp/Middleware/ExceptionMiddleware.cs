using BankApiApp.DTOs;
using BankApiApp.DTOs.Common;
using BankApiApp.Exceptions;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BankApiApp.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
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

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // status kodu xetanin tipine gore secilir
        var statusCode = ex switch
        {
            AppException appEx => appEx.StatusCode,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        // 500 = real bug → loglayiriq; toqqusma ve biznes xetalari normaldir
        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Gozlenilmeyen xeta bas verdi.");

        // istifadeciye geden mesaj
        var message = statusCode switch
        {
            StatusCodes.Status500InternalServerError => "Daxili server xetasi bas verdi.",
            StatusCodes.Status409Conflict =>
                "Emeliyyat eyni anda basqa deyisiklikle toqqusdu. Zehmet olmasa yeniden cehd edin.",
            _ => ex.Message
        };

        var response = new ErrorResponseDto
        {
            StatusCode = statusCode,
            Error = message
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}