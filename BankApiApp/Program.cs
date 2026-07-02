using BankApiApp.Data;
using BankApiApp.Extensions;
using BankApiApp.Middleware;
using BankApiApp.Services;
using Hangfire;


QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
var builder = WebApplication.CreateBuilder(args);

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

app.UseHangfireDashboard("/hangfire");

// Tekrarlanan is: her gun vaxti catmis kredit odenislerini tut
RecurringJob.AddOrUpdate<ILoanService>(
    "process-loan-payments",
    service => service.ProcessDuePaymentsAsync(),
    Cron.Daily);

app.UseAuthentication();
app.UseAuthorization();
app.MapHub<BankApiApp.Hubs.ChatHub>("/chatHub");
app.MapControllers();
app.Run();