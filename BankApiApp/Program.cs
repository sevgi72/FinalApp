using BankApiApp.Data;
using BankApiApp.Extensions;
using BankApiApp.Services;
using Hangfire;

var builder = WebApplication.CreateBuilder(args);

// Bütün servis qeydiyyatları bir sətirdə
builder.Services.AddApplicationServices(builder.Configuration);

var app = builder.Build();

// Rolları və admin istifadəçisini seed et
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

// Təkrarlanan iş: hər gün vaxtı çatmış kredit ödənişlərini tut
RecurringJob.AddOrUpdate<ILoanService>(
    "process-loan-payments",
    service => service.ProcessDuePaymentsAsync(),
    Cron.Daily);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();