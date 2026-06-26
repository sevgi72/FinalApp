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
        // status kodu xətanın tipinə görə seçilir
        var statusCode = ex switch
        {
            AppException appEx => appEx.StatusCode,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        // 500 = real bug → loglayırıq; toqquşma və biznes xətaları normaldır
        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Gözlənilməyən xəta baş verdi.");

        // istifadəçiyə gedən mesaj
        var message = statusCode switch
        {
            StatusCodes.Status500InternalServerError => "Daxili server xətası baş verdi.",
            StatusCodes.Status409Conflict =>
                "Əməliyyat eyni anda başqa dəyişikliklə toqquşdu. Zəhmət olmasa yenidən cəhd edin.",
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