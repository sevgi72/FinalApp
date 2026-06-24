using BankApiApp.DTOs;
using BankApiApp.Exceptions;
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
            // sorğunu növbəti mərhələyə ötür (controller-ə çatır)
            await _next(context);
        }
        catch (Exception ex)
        {
            // hər hansı yerdə atılan xəta bura düşür
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // bizim öz xətalarımız status kodu daşıyır; naməlum xəta → 500
        var statusCode = ex is AppException appEx
            ? appEx.StatusCode
            : StatusCodes.Status500InternalServerError;

        // 500 = real bug → loglayırıq və detalını gizlədirik (təhlükəsizlik)
        if (statusCode == StatusCodes.Status500InternalServerError)
            _logger.LogError(ex, "Gözlənilməyən xəta baş verdi.");

        var message = statusCode == StatusCodes.Status500InternalServerError
            ? "Daxili server xətası baş verdi."
            : ex.Message;

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