using BankApiApp.Data;
using BankApiApp.Extensions;
using BankApiApp.Middleware;
using BankApiApp.Services;
using Hangfire;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// ══════════════ CORS ══════════════
// Frontend (Live Server, React) backend-e qosula bilsin.
// SetIsOriginAllowed(_ => true) - her menbeye icaze (development ucun).
// Deploy zamani WithOrigins("https://amorabank.az") ile evez edilmelidir.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();             // SignalR ucun (token oturulur)
    });
});

// ══════════════ RATE LIMITING ══════════════
// brute-force ve flood qarsisi
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // qlobal limit: her IP ucun deqiqede 100 sorgu
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));

    // "auth" siyaseti: login/register kimi hessas endpointler — deqiqede 5 cehd
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"statusCode\":429,\"error\":\"Cox sayda cehd. Bir az sonra yeniden yoxlayin.\"}",
            cancellationToken);
    };
});

// ══════════════ HEALTH CHECK ══════════════
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: new[] { "db" });

// ══════════════ SERVISLER ══════════════
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSignalR();

// QEYD: lokalizasiya JSON esasli oz servisimizle isleyir (ILocalizationService).
// Kohne .resx qeydiyyatlari (AddLocalization / UseRequestLocalization) silindi.

var app = builder.Build();

// ══════════════ PIPELINE ══════════════
// Sira vacibdir: Exception → Swagger → CORS → RateLimiter → Auth

app.UseMiddleware<ExceptionMiddleware>();

// Rollari, admin/superadmin ve komissiya qaydalarini seed et
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS authentication-dan EVVEL olmalidir (preflight sorgular ucun)
app.UseCors("AllowAll");

// rate limiter — authentication-dan evvel islemelidir
app.UseRateLimiter();

app.UseAuthentication();   // token-i oxuyur, istifadecini taniyir
app.UseAuthorization();    // rolu yoxlayir

// ══════════════ HANGFIRE ══════════════
// dashboard rate limit-den azad (Trigger now basanda 429 olmasin)
app.UseHangfireDashboard("/hangfire");

// Tekrarlanan isler
RecurringJob.AddOrUpdate<ILoanService>(
    "process-loan-payments",
    service => service.ProcessDuePaymentsAsync(),
    Cron.Daily);

RecurringJob.AddOrUpdate<ICleanupService>(
    "cleanup-unconfirmed-accounts",
    service => service.CleanupUnconfirmedAccountsAsync(),
    Cron.Daily);

RecurringJob.AddOrUpdate<IScheduledPaymentService>(
    "process-scheduled-payments",
    service => service.ProcessDuePaymentsAsync(),
    Cron.Daily);

RecurringJob.AddOrUpdate<ICashByCodeService>(
    "expire-cash-by-code",
    service => service.ExpireOldCodesAsync(),
    Cron.Hourly);

RecurringJob.AddOrUpdate<IDepositService>(
    "accrue-deposit-interest",
    service => service.AccrueInterestAsync(),
    Cron.Monthly);

// ══════════════ ENDPOINT-LER ══════════════

// /health — rate limit-den azaddir (monitorinq tez-tez cagirir)
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            totalDuration = report.TotalDuration.TotalMilliseconds + " ms",
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration.TotalMilliseconds + " ms"
            })
        });

        await context.Response.WriteAsync(result);
    }
}).DisableRateLimiting();

// SignalR hub-lari rate limit-den azad (canli baglanti limiti yemesin)
app.MapHub<BankApiApp.Hubs.NotificationHub>("/notificationHub").DisableRateLimiting();
app.MapHub<BankApiApp.Hubs.ChatHub>("/chatHub").DisableRateLimiting();

app.MapControllers();

app.Run();