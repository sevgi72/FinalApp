using BankApiApp.Data;
using BankApiApp.DTOs.SavingsGoal;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// Yigim meqsedleri — xususile hedef azaldilanda artiq meblegin
/// hesaba qaytarilmasi meqami (pul itmemelidir).
/// </summary>
public class SavingsGoalServiceTests
{
    private const string UserId = "user-1";

    private readonly Mock<ILocalizationService> _localizer = new();
    private readonly Mock<INotificationService> _notificationService = new();

    public SavingsGoalServiceTests()
    {
        _localizer.Setup(x => x.Get(It.IsAny<string>())).Returns((string key) => key);
    }

    private SavingsGoalService CreateSut(AppDbContext db) =>
        new(db, _localizer.Object, _notificationService.Object);

    private static Account Account(decimal balance = 1000) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = UserId,
        Balance = balance,
        AccountNumber = "AZ0000000000000001",
        Status = AccountStatus.Active
    };

    private static SavingsGoal Goal(Guid accountId, decimal target = 1000, decimal current = 0) => new()
    {
        Id = Guid.NewGuid(),
        UserId = UserId,
        Name = "Noutbuk",
        TargetAmount = target,
        CurrentAmount = current,
        SourceAccountId = accountId,
        IsCompleted = current >= target
    };


    /* ══════════════ CONTRIBUTE ══════════════ */

    [Fact]
    public async Task ContributeAsync_BalansKifayetEtmirse_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 50);
        var goal = Goal(acc.Id);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.ContributeAsync(UserId, goal.Id, new ContributeGoalDto { Amount = 100 });

        await act.Should().ThrowAsync<BadRequestException>();
        acc.Balance.Should().Be(50);
        goal.CurrentAmount.Should().Be(0);
    }

    [Fact]
    public async Task ContributeAsync_Ugurlu_PulHesabdanMeqsedeKecir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 1000);
        var goal = Goal(acc.Id, target: 500);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ContributeAsync(UserId, goal.Id, new ContributeGoalDto { Amount = 200 });

        acc.Balance.Should().Be(800);
        goal.CurrentAmount.Should().Be(200);
        goal.IsCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task ContributeAsync_HedefeCatanda_TamamlanmisIsarelenirVeBildirisGedir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 1000);
        var goal = Goal(acc.Id, target: 500, current: 400);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ContributeAsync(UserId, goal.Id, new ContributeGoalDto { Amount = 100 });

        goal.CurrentAmount.Should().Be(500);
        goal.IsCompleted.Should().BeTrue();

        // istifadeci tebrik bildirisi almalidir
        _notificationService.Verify(x => x.SendToUserAsync(
            UserId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ContributeAsync_MeqsedArtiqTamamlanibsa_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var goal = Goal(acc.Id, target: 500, current: 500);   // IsCompleted = true
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.ContributeAsync(UserId, goal.Id, new ContributeGoalDto { Amount = 50 });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task ContributeAsync_BasqasininMeqsedi_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var goal = Goal(acc.Id);
        goal.UserId = "basqa-user";
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // TEHLUKESIZLIK: basqasinin meqsedine mudaxile olmamalidir
        var act = async () => await sut.ContributeAsync(UserId, goal.Id, new ContributeGoalDto { Amount = 50 });

        await act.Should().ThrowAsync<NotFoundException>();
    }


    /* ══════════════ WITHDRAW ══════════════ */

    [Fact]
    public async Task WithdrawAsync_MeqseddekiMeblegdenCox_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 0);
        var goal = Goal(acc.Id, current: 100);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.WithdrawAsync(UserId, goal.Id, new ContributeGoalDto { Amount = 101 });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task WithdrawAsync_TamamlanmisMeqseddenCixaranda_StatusGeriAlinir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 0);
        var goal = Goal(acc.Id, target: 500, current: 500);
        goal.IsCompleted = true;
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.WithdrawAsync(UserId, goal.Id, new ContributeGoalDto { Amount = 100 });

        goal.CurrentAmount.Should().Be(400);
        goal.IsCompleted.Should().BeFalse();   // artiq hedefde deyil
        acc.Balance.Should().Be(100);
    }


    /* ══════════════ UPDATE (hedef azaldilanda refund) ══════════════ */

    [Fact]
    public async Task UpdateAsync_HedefAzaldilanda_ArtiqMeblegHesabaQaytarilir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 0);
        var goal = Goal(acc.Id, target: 1000, current: 500);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // hedef 500-den 300-e dusur → 200 artiq qalir
        await sut.UpdateAsync(UserId, goal.Id, new UpdateSavingsGoalDto
        {
            Name = "Noutbuk",
            TargetAmount = 300
        });

        acc.Balance.Should().Be(200);          // artiq mebleg qayitdi
        goal.CurrentAmount.Should().Be(300);   // meqsed tam dolu
        goal.TargetAmount.Should().Be(300);
        goal.IsCompleted.Should().BeTrue();

        // pul herekati tarixceye dusmelidir (F4)
        var trx = db.Transactions.Single();
        trx.Amount.Should().Be(200);
        trx.ToBalanceSnapshot.Should().Be(200);

        // istifadeci xeberdar edilmelidir
        _notificationService.Verify(x => x.SendToUserAsync(
            UserId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_HedefArtirilanda_PulQaytarilmirVeStatusAcilir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        var goal = Goal(acc.Id, target: 500, current: 500);
        goal.IsCompleted = true;
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.UpdateAsync(UserId, goal.Id, new UpdateSavingsGoalDto
        {
            Name = "Noutbuk",
            TargetAmount = 800
        });

        acc.Balance.Should().Be(100);          // deyismir
        goal.CurrentAmount.Should().Be(500);   // deyismir
        goal.IsCompleted.Should().BeFalse();   // artiq hedefe catmayib
        db.Transactions.Should().BeEmpty();    // pul herekati yoxdur
    }

    [Fact]
    public async Task UpdateAsync_HedefToplanandanCoxdursa_PulQaytarilmir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 0);
        var goal = Goal(acc.Id, target: 1000, current: 300);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // 500 > 300 (toplanan) → refund olmamalidir
        await sut.UpdateAsync(UserId, goal.Id, new UpdateSavingsGoalDto
        {
            Name = "Noutbuk",
            TargetAmount = 500
        });

        acc.Balance.Should().Be(0);
        goal.CurrentAmount.Should().Be(300);
        db.Transactions.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_AdBosdursa_XetaAtir(string name)
    {
        using var db = TestDb.Create();
        var acc = Account();
        var goal = Goal(acc.Id);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.UpdateAsync(UserId, goal.Id, new UpdateSavingsGoalDto
        {
            Name = name,
            TargetAmount = 500
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task UpdateAsync_HedefMusbetDeyilse_XetaAtir(decimal target)
    {
        using var db = TestDb.Create();
        var acc = Account();
        var goal = Goal(acc.Id);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.UpdateAsync(UserId, goal.Id, new UpdateSavingsGoalDto
        {
            Name = "Noutbuk",
            TargetAmount = target
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }


    /* ══════════════ DELETE ══════════════ */

    [Fact]
    public async Task DeleteAsync_IcindePulVarsa_HesabaQaytarilir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        var goal = Goal(acc.Id, current: 350);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.DeleteAsync(UserId, goal.Id);

        acc.Balance.Should().Be(450);          // 100 + 350
        db.SavingsGoals.Should().BeEmpty();
        db.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteAsync_MeqsedBosdursa_PulHerekatiOlmur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        var goal = Goal(acc.Id, current: 0);
        db.Accounts.Add(acc);
        db.SavingsGoals.Add(goal);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.DeleteAsync(UserId, goal.Id);

        acc.Balance.Should().Be(100);
        db.Transactions.Should().BeEmpty();
    }
}