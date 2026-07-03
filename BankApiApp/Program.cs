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
app.UseAuthentication(); // Tokeni oxuyur və istifadəçini tanıyır
app.UseAuthorization();  // İstifadəçinin rolunu yoxlayır

// 2. Sonra isə panellər və endpoint-lər gəlməlidir
app.UseHangfireDashboard("/hangfire");

// Tekrarlanan iş
RecurringJob.AddOrUpdate<ILoanService>(
    "process-loan-payments",
    service => service.ProcessDuePaymentsAsync(),
    Cron.Daily);

// 3. Ən sonda Hub-lar və Controller-lər xəritələnməlidir
app.MapHub<BankApiApp.Hubs.ChatHub>("/chatHub");
app.MapControllers();

app.Run(); 