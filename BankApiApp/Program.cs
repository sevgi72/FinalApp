using BankApiApp.Data;
using BankApiApp.Extensions;
using BankApiApp.Middleware;
using BankApiApp.Services;
using Hangfire;


QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);
// CORS — kənar səhifələr (çat, frontend) backend-ə qoşula bilsin
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)   // hər mənbəyə icazə
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();             // SignalR üçün (token ötürülür)
    });
});
// Butun servis qeydiyyatlari bir setirde
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddSignalR();
// lokalizasiya — .resx fayllarını Resources qovluğundan oxu
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// dəstəklənən dillər + default
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
// istifadəçinin Accept-Language header-inə görə dili təyin et
var locOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(locOptions.Value);
app.UseAuthentication(); // Tokeni oxuyur və istifadəçini tanıyır
app.UseAuthorization();  // İstifadəçinin rolunu yoxlayır

// 2. Sonra isə panellər və endpoint-lər gəlməlidir
app.UseHangfireDashboard("/hangfire");

// Tekrarlanan iş
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
// 3. Ən sonda Hub-lar və Controller-lər xəritələnməlidir
app.MapHub<BankApiApp.Hubs.ChatHub>("/chatHub");
app.MapControllers();

app.Run(); 