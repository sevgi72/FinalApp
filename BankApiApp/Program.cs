using BankApiApp.Data;
using BankApiApp.Extensions;
using BankApiApp.Middleware;
using BankApiApp.Services;
using Hangfire;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// CORS — kenar sehifeler (cat, frontend) backend-e qosula bilsen
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)   // her menbeye icaze
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();             // SignalR ucun (token oturulur)
    });
});

// rate limiting — brute-force ve flood qarsisibu
builder.Services.AddRateLimiter(options =>
{
    // limit asilanda 429 qaytar
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

    // 429 cavabina mesaj elave et
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"statusCode\":429,\"error\":\"Cox sayda cehd. Bir az sonra yeniden yoxlayin.\"}",
            cancellationToken);
    };
});

// Health check — sistemin sagligini yoxlayir (DB daxil)
builder.Services.AddHealthChecks()
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "postgresql",
        tags: new[] { "db" });

// Butun servis qeydiyyatlari bir setirde
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSignalR();

// lokalizasiya — .resx fayllarini Resources qovlugundan oxu
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// desteklenen diller + default
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = new[] { "az", "en", "ru" };
    options.SetDefaultCulture("az");
    options.AddSupportedCultures(cultures);
    options.AddSupportedUICultures(cultures);
});

var app = builder.Build();

app.UseMiddleware<ExceptionMiddleware>();

// Rollari ve admin istifadecisini seed et
using (var scope = app.Services.CreateScope())
{
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");

// rate limiter — authentication-dan evvel islemelidir
app.UseRateLimiter();

// istifadecinin Accept-Language header-ine gore dili teyin et
var locOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);

app.UseAuthentication(); // Tokeni oxuyur ve istifadecini taniyir
app.UseAuthorization();  // Istifadecinin rolunu yoxlayir

// 2. Sonra ise paneller ve endpoint-ler gelmelidir
app.UseHangfireDashboard("/hangfire");

// Tekrarlanan isler (Hangfire)
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
// 3. En sonda Hub-lar, Controller-ler ve Health Check-ler xeritelenmelidir

// /health — sistemin veziyyeti (JSON cavab qaytarir ve Rate Limiting-den azaddir)
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";

        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),                  // Healthy / Degraded / Unhealthy
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
app.MapHub<BankApiApp.Hubs.NotificationHub>("/notificationHub");
app.MapHub<BankApiApp.Hubs.ChatHub>("/chatHub");
app.MapControllers();

app.Run();