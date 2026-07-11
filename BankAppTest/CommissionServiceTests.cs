using BankApiApp.Data;
using BankApiApp.Models;
using BankApiApp.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankAppTest;

public class CommissionServiceTests
{
    // Her test ucun teze, tertemiz InMemory baza yaradir.
    // Unique ad veririk ki, testler bir-birine qarismasin.
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    // ── TEST 1: Qayda yoxdursa, komissiya 0 olmalidir ──
    [Fact]
    public async Task CalculateAsync_QaydaYoxdursa_SifirQaytarir()
    {
        // Arrange (hazirliq): bos baza, hec bir qayda yoxdur
        using var context = CreateContext();
        var service = new CommissionService(context);

        // Act (icra): 1000 AZN-lik Transfer ucun komissiya hesabla
        var result = await service.CalculateAsync("Transfer", 1000m);

        // Assert (yoxlama): qayda olmadigi ucun 0 olmalidir
        result.Should().Be(0);
    }

    // ── TEST 2: Meblег minimumdan asagidirsa, komissiya 0 olmalidir ──
    [Fact]
    public async Task CalculateAsync_MeblegMinimumdanAsagidirsa_SifirQaytarir()
    {
        // Arrange: qayda var - 100 AZN-den yuxari 1% tutulur
        using var context = CreateContext();
        context.CommissionRules.Add(new CommissionRule
        {
            TransactionType = "Transfer",
            MinAmount = 100m,
            Percentage = 0.01m,     // 1%
            MaxCommission = 50m,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var service = new CommissionService(context);

        // Act: 50 AZN (minimumdan asagi) ucun komissiya hesabla
        var result = await service.CalculateAsync("Transfer", 50m);

        // Assert: 100-den asagi oldugu ucun komissiya yoxdur
        result.Should().Be(0);
    }

    // ── TEST 3: Normal halda duz faiz hesablanmalidir ──
    [Fact]
    public async Task CalculateAsync_NormalHal_DuzFaizHesablayir()
    {
        // Arrange: 100 AZN-den yuxari 1% komissiya
        using var context = CreateContext();
        context.CommissionRules.Add(new CommissionRule
        {
            TransactionType = "Transfer",
            MinAmount = 100m,
            Percentage = 0.01m,     // 1%
            MaxCommission = 50m,
            IsActive = true
        });
        await context.SaveChangesAsync();
        var service = new CommissionService(context);

        // Act: 1000 AZN ucun komissiya hesabla
        var result = await service.CalculateAsync("Transfer", 1000m);

        // Assert: 1000 * 1% = 10 AZN
        result.Should().Be(10m);
    }

    // ── TEST 4: Komissiya tavani asirsa, tavana enmelidir ──
    [Fact]
    public async Task CalculateAsync_TavaniAsir_TavanaEnir()
    {
        // Arrange: 1% komissiya, amma tavan 50 AZN
        using var context = CreateContext();
        context.CommissionRules.Add(new CommissionRule
        {
            TransactionType = "Transfer",
            MinAmount = 100m,
            Percentage = 0.01m,     // 1%
            MaxCommission = 50m,    // maksimum 50 AZN
            IsActive = true
        });
        await context.SaveChangesAsync();
        var service = new CommissionService(context);

        // Act: 10000 AZN ucun komissiya (1% = 100 AZN, amma tavan 50)
        var result = await service.CalculateAsync("Transfer", 10000m);

        // Assert: 100 hesablanir amma tavan 50-dir → 50 qaytarilir
        result.Should().Be(50m);
    }

    // ── TEST 5: Qayda passivdirse (IsActive=false), komissiya 0 olmalidir ──
    [Fact]
    public async Task CalculateAsync_QaydaPassivdirse_SifirQaytarir()
    {
        // Arrange: qayda var amma sondurulub
        using var context = CreateContext();
        context.CommissionRules.Add(new CommissionRule
        {
            TransactionType = "Transfer",
            MinAmount = 100m,
            Percentage = 0.01m,
            MaxCommission = 50m,
            IsActive = false        // ← passiv
        });
        await context.SaveChangesAsync();
        var service = new CommissionService(context);

        // Act
        var result = await service.CalculateAsync("Transfer", 1000m);

        // Assert: passiv qayda hesaba alinmir → 0
        result.Should().Be(0);
    }
}