using BankApiApp.Data;
using BankApiApp.DTOs.Utility;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// Planlanmis (avtomatik) kommunal odenisler — Hangfire her gun cagirir.
/// En hessas qayda: eyni ay ICINDE tekrar odenis olmamalidir
/// (LastExecutedAt < ayin baslangici yoxlamasi).
/// </summary>
public class ScheduledPaymentServiceTests
{
    private const string UserId = "user-1";

    private readonly Mock<ILocalizationService> _localizer = new();
    private readonly Mock<ILogger<ScheduledPaymentService>> _logger = new();

    public ScheduledPaymentServiceTests()
    {
        _localizer.Setup(x => x.Get(It.IsAny<string>())).Returns((string key) => key);
    }

    private ScheduledPaymentService CreateSut(AppDbContext db) =>
        new(db, _localizer.Object, _logger.Object);

    private static Account Account(decimal balance = 1000, string ownerId = UserId,
        AccountStatus status = AccountStatus.Active) => new()
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Balance = balance,
            AccountNumber = "AZ0000000000000001",
            Status = status
        };

    private static UtilityProvider Provider(bool isActive = true) => new()
    {
        Id = Guid.NewGuid(),
        Name = "Azerisiq",
        IsActive = isActive
    };

    private static ScheduledPayment Scheduled(Guid accountId, Guid providerId,
        int dayOfMonth, decimal amount = 50, bool isActive = true,
        DateTime? lastExecuted = null) => new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            AccountId = accountId,
            ProviderId = providerId,
            SubscriberCode = "12345678",
            Amount = amount,
            DayOfMonth = dayOfMonth,
            IsActive = isActive,
            LastExecutedAt = lastExecuted
        };

    // bugunku gun — testler "bu gun icra olunmalidir" halini qursun
    private static int Today => DateTime.UtcNow.Day;


    /* ══════════════ YARATMA ══════════════ */

    [Fact]
    public async Task CreateAsync_BasqasininHesabi_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(ownerId: "basqa-user");
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.CreateAsync(UserId, new CreateScheduledPaymentDto
        {
            AccountId = acc.Id,
            ProviderId = provider.Id,
            SubscriberCode = "12345678",
            Amount = 50,
            DayOfMonth = 5
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_ProvayderDeaktivdirse_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var provider = Provider(isActive: false);
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.CreateAsync(UserId, new CreateScheduledPaymentDto
        {
            AccountId = acc.Id,
            ProviderId = provider.Id,
            SubscriberCode = "12345678",
            Amount = 50,
            DayOfMonth = 5
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateAsync_Ugurlu_AktivQaydaYaradilir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.CreateAsync(UserId, new CreateScheduledPaymentDto
        {
            AccountId = acc.Id,
            ProviderId = provider.Id,
            SubscriberCode = "12345678",
            Amount = 50,
            DayOfMonth = 15
        });

        result.IsActive.Should().BeTrue();
        result.LastExecutedAt.Should().BeNull();   // hele icra olunmayib

        var sp = db.ScheduledPayments.Single();
        sp.DayOfMonth.Should().Be(15);
        sp.UserId.Should().Be(UserId);
    }


    /* ══════════════ SONDURME / SILME ══════════════ */

    [Fact]
    public async Task ToggleAsync_BasqasininQaydasi_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var provider = Provider();
        var sp = Scheduled(acc.Id, provider.Id, 5);
        sp.UserId = "basqa-user";
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        db.ScheduledPayments.Add(sp);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.ToggleAsync(UserId, sp.Id, false);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_Ugurlu_QaydaSilinir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var provider = Provider();
        var sp = Scheduled(acc.Id, provider.Id, 5);
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        db.ScheduledPayments.Add(sp);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.DeleteAsync(UserId, sp.Id);

        db.ScheduledPayments.Should().BeEmpty();
    }


    /* ══════════════ AVTOMATIK ICRA (Hangfire) ══════════════ */

    [Fact]
    public async Task ProcessDuePaymentsAsync_BugunOdenmelidirse_IcraOlunur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        db.ScheduledPayments.Add(Scheduled(acc.Id, provider.Id, Today, amount: 50));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        acc.Balance.Should().Be(450);
        db.UtilityPayments.Should().HaveCount(1);
        db.Transactions.Should().HaveCount(1);
        db.ScheduledPayments.Single().LastExecutedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_BuAyArtiqIcraOlunubsa_TekrarOdenilmir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);

        // bu ayin evvelinde artiq icra olunub
        var thisMonthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        db.ScheduledPayments.Add(Scheduled(acc.Id, provider.Id, Today,
            lastExecuted: thisMonthStart.AddHours(2)));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        // KRITIK: Hangfire gunde bir defe islese de, tekrar tutulmamalidir
        acc.Balance.Should().Be(500);
        db.UtilityPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_KecenAyIcraOlunubsa_YenidenOdenilir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);

        // sonuncu icra kecen ay olub → yeni ay ucun yeniden odenmelidir
        var lastMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddDays(-5);
        db.ScheduledPayments.Add(Scheduled(acc.Id, provider.Id, Today, lastExecuted: lastMonth));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        acc.Balance.Should().Be(450);
        db.UtilityPayments.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_BasqaGununQaydasi_Toxunulmur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);

        // bugunden ferqli gun sec
        var otherDay = Today == 1 ? 2 : 1;
        db.ScheduledPayments.Add(Scheduled(acc.Id, provider.Id, otherDay));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        acc.Balance.Should().Be(500);
        db.UtilityPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_QaydaSondurulubse_Toxunulmur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        db.ScheduledPayments.Add(Scheduled(acc.Id, provider.Id, Today, isActive: false));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        acc.Balance.Should().Be(500);
        db.UtilityPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_BalansKifayetEtmirse_Atlanir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 30);   // 50 lazimdir
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        db.ScheduledPayments.Add(Scheduled(acc.Id, provider.Id, Today, amount: 50));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        // balans MENFIYE dusmemelidir
        acc.Balance.Should().Be(30);
        db.UtilityPayments.Should().BeEmpty();
        // LastExecutedAt teyin olunmamalidir — novbeti defe yeniden cehd edilsin
        db.ScheduledPayments.Single().LastExecutedAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_HesabDeaktivdirse_Atlanir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500, status: AccountStatus.Blocked);
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        db.ScheduledPayments.Add(Scheduled(acc.Id, provider.Id, Today));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        acc.Balance.Should().Be(500);
        db.UtilityPayments.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_BirQaydaAtlansaDaDigerleriIcraOlunur()
    {
        using var db = TestDb.Create();
        var poorAccount = Account(balance: 10);      // catmir
        var richAccount = Account(balance: 500);     // catir
        var provider = Provider();
        db.Accounts.AddRange(poorAccount, richAccount);
        db.UtilityProviders.Add(provider);
        db.ScheduledPayments.Add(Scheduled(poorAccount.Id, provider.Id, Today, amount: 50));
        db.ScheduledPayments.Add(Scheduled(richAccount.Id, provider.Id, Today, amount: 50));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        // bir qaydanin atlanmasi digerini dayandirmamalidir
        poorAccount.Balance.Should().Be(10);
        richAccount.Balance.Should().Be(450);
        db.UtilityPayments.Should().HaveCount(1);
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_IcraOlunanda_TarixceyeQeydDuser()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var provider = Provider();
        db.Accounts.Add(acc);
        db.UtilityProviders.Add(provider);
        db.ScheduledPayments.Add(Scheduled(acc.Id, provider.Id, Today, amount: 50));
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        var trx = db.Transactions.Single();
        trx.Type.Should().Be(TransactionType.Withdraw);
        trx.Amount.Should().Be(50);
        trx.FromAccountId.Should().Be(acc.Id);
        trx.Description.Should().Contain("avtomatik");
    }
}