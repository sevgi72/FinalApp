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

// rate limiting — brute-force ve flood qarsisi
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

// Tekrarlanan is
RecurringJob.AddOrUpdate<ILoanService>(
    "process-loan-payments",
    service => service.ProcessDuePaymentsAsync(),
    Cron.Daily);

// Tekrarlanan is: tesdiqlenmemis kohne hesablari temizle
RecurringJob.AddOrUpdate<ICleanupService>(
    "cleanup-unconfirmed-accounts",
    service => service.CleanupUnconfirmedAccountsAsync(),
    Cron.Daily);

// Tekrarlanan is: planlanmis kommunal odenisler
RecurringJob.AddOrUpdate<IScheduledPaymentService>(
    "process-scheduled-payments",
    service => service.ProcessDuePaymentsAsync(),
    Cron.Daily);

// Tekrarlanan is: vaxti kecmis kodla kocurmeleri qaytar
RecurringJob.AddOrUpdate<ICashByCodeService>(
    "expire-cash-by-code",
    service => service.ExpireOldCodesAsync(),
    Cron.Hourly);

// 3. En sonda Hub-lar ve Controller-ler xeritelenmelidir
app.MapHub<BankApiApp.Hubs.ChatHub>("/chatHub");
app.MapControllers();

app.Run();