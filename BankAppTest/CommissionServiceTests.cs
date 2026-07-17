using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// Komissiya hesablanmasi — bankin en hessas biznes qaydalarindan biridir.
/// Sehv hesablama birbasa pul itkisi demekdir, ona gore serhed hallari test olunur.
/// </summary>
public class CommissionServiceTests
{
    // testlerde tekrarlanan qayda yaratma
    private static CommissionRule Rule(
        string type = "Transfer",
        decimal minAmount = 100,
        decimal percentage = 0.02m,   // 2%
        decimal maxCommission = 50,
        bool isActive = true) => new()
        {
            TransactionType = type,
            MinAmount = minAmount,
            Percentage = percentage,
            MaxCommission = maxCommission,
            IsActive = isActive
        };

    [Fact]
    public async Task CalculateAsync_QaydaYoxdursa_SifirQaytarir()
    {
        // Arrange — bazada hec bir qayda yoxdur
        using var db = TestDb.Create();
        var sut = new CommissionService(db);

        // Act
        var result = await sut.CalculateAsync("Transfer", 1000);

        // Assert — qayda yoxdursa komissiya tutulmamalidir
        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateAsync_QaydaSondurulubse_SifirQaytarir()
    {
        using var db = TestDb.Create();
        db.CommissionRules.Add(Rule(isActive: false));
        await db.SaveChangesAsync();
        var sut = new CommissionService(db);

        var result = await sut.CalculateAsync("Transfer", 1000);

        // IsActive = false → qayda tetbiq olunmur
        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateAsync_MeblegMinimumdanAzdirsa_SifirQaytarir()
    {
        using var db = TestDb.Create();
        db.CommissionRules.Add(Rule(minAmount: 100));
        await db.SaveChangesAsync();
        var sut = new CommissionService(db);

        // 99 < 100 (MinAmount) → komissiya yoxdur
        var result = await sut.CalculateAsync("Transfer", 99);

        result.Should().Be(0);
    }

    [Fact]
    public async Task CalculateAsync_MeblegMinimumaBeraberdirse_KomissiyaTutulur()
    {
        using var db = TestDb.Create();
        db.CommissionRules.Add(Rule(minAmount: 100, percentage: 0.02m));
        await db.SaveChangesAsync();
        var sut = new CommissionService(db);

        // serhed hali: mebleg tam minimuma beraberdir → qayda ISLEMELIDIR
        var result = await sut.CalculateAsync("Transfer", 100);

        result.Should().Be(2m);   // 100 * 0.02
    }

    [Fact]
    public async Task CalculateAsync_NormalMebleg_FaiziDuzgunHesablayir()
    {
        using var db = TestDb.Create();
        db.CommissionRules.Add(Rule(percentage: 0.02m, maxCommission: 50));
        await db.SaveChangesAsync();
        var sut = new CommissionService(db);

        var result = await sut.CalculateAsync("Transfer", 1000);

        result.Should().Be(20m);   // 1000 * 0.02
    }

    [Fact]
    public async Task CalculateAsync_TavaniAsirsa_TavanQaytarir()
    {
        using var db = TestDb.Create();
        db.CommissionRules.Add(Rule(percentage: 0.02m, maxCommission: 50));
        await db.SaveChangesAsync();
        var sut = new CommissionService(db);

        // 10000 * 0.02 = 200, amma tavan 50-dir
        var result = await sut.CalculateAsync("Transfer", 10000);

        result.Should().Be(50m);
    }

    [Fact]
    public async Task CalculateAsync_TavanSifirdirsa_TavanTetbiqOlunmur()
    {
        using var db = TestDb.Create();
        // MaxCommission = 0 → "tavan yoxdur" demekdir
        db.CommissionRules.Add(Rule(percentage: 0.02m, maxCommission: 0));
        await db.SaveChangesAsync();
        var sut = new CommissionService(db);

        var result = await sut.CalculateAsync("Transfer", 10000);

        result.Should().Be(200m);   // limitlenmir
    }

    [Fact]
    public async Task CalculateAsync_Neticeni2OnlukBasamagaYuvarlaqlasdirir()
    {
        using var db = TestDb.Create();
        db.CommissionRules.Add(Rule(minAmount: 1, percentage: 0.015m, maxCommission: 0));
        await db.SaveChangesAsync();
        var sut = new CommissionService(db);

        // 33.33 * 0.015 = 0.49995 → 0.50
        var result = await sut.CalculateAsync("Transfer", 33.33m);

        result.Should().Be(0.50m);
    }

    [Fact]
    public async Task CalculateAsync_BasqaEmeliyyatNovu_OQaydaniIslemir()
    {
        using var db = TestDb.Create();
        db.CommissionRules.Add(Rule(type: "Transfer"));
        await db.SaveChangesAsync();
        var sut = new CommissionService(db);

        // qayda yalniz "Transfer" ucundur — "Withdraw" ucun tapilmamalidir
        var result = await sut.CalculateAsync("Withdraw", 1000);

        result.Should().Be(0);
    }
}