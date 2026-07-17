using BankApiApp.Data;
using BankApiApp.DTOs.Statement;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// F5 — hesab cixarisi.
/// En incə hisse: acilis balansi. O, dovrden EVVELki butun emeliyyatlarin
/// cemi kimi hesablanir; sehv olsa butun cixaris setirleri surusur.
/// </summary>
public class StatementServiceTests
{
    private const string UserId = "user-1";

    private readonly Mock<ILocalizationService> _localizer = new();

    public StatementServiceTests()
    {
        _localizer.Setup(x => x.Get(It.IsAny<string>())).Returns((string key) => key);
    }

    private StatementService CreateSut(AppDbContext db) => new(db, _localizer.Object);

    private static Account Account(decimal balance = 1000, string ownerId = UserId) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        Balance = balance,
        AccountNumber = "AZ0000000000000001",
        Status = AccountStatus.Active
    };

    /// <summary>
    /// Emeliyyati ISTENILEN tarixle bazaya elave edir.
    ///
    /// AppDbContext.SaveChangesAsync() CreatedAt-i "Added" veziyyetinde
    /// avtomatik UtcNow ile doldurur — yeni birbasa teyin etdiyimiz tarix
    /// uzerine yazilir. Ona gore iki merhele isledirik:
    ///   1) elave et + saxla     → CreatedAt = indi (avtomatik damga)
    ///   2) tarixi duzelt + saxla → veziyyet "Modified"-dir; orada
    ///      CreatedAt-e TOXUNULMUR, yalniz UpdatedAt yenilenir.
    /// Beleliklə kohne tarixli emeliyyatlari test ede bilirik.
    /// </summary>
    private static async Task AddTxAsync(AppDbContext db, Transaction tx, DateTime when)
    {
        db.Transactions.Add(tx);
        await db.SaveChangesAsync();

        tx.CreatedAt = when;
        await db.SaveChangesAsync();
    }

    // daxil olan emeliyyat
    private static Transaction In(Guid accountId, decimal amount) => new()
    {
        Amount = amount,
        Type = TransactionType.Deposit,
        Status = TransactionStatus.Completed,
        ToAccountId = accountId,
        Description = "Daxil"
    };

    // cixan emeliyyat
    private static Transaction Out(Guid accountId, decimal amount) => new()
    {
        Amount = amount,
        Type = TransactionType.Withdraw,
        Status = TransactionStatus.Completed,
        FromAccountId = accountId,
        Description = "Cixan"
    };

    private static StatementRequestDto Request(Guid accountId, DateTime from, DateTime to) => new()
    {
        AccountId = accountId,
        From = from,
        To = to
    };


    /* ══════════════ VALIDASIYA ══════════════ */

    [Fact]
    public async Task GenerateAsync_HesabTapilmirsa_XetaAtir()
    {
        using var db = TestDb.Create();
        var sut = CreateSut(db);

        var act = async () => await sut.GenerateAsync(UserId,
            Request(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GenerateAsync_BasqasininHesabi_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(ownerId: "basqa-user");
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // TEHLUKESIZLIK: basqasinin cixarisini almaq olmaz
        var act = async () => await sut.GenerateAsync(UserId,
            Request(acc.Id, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GenerateAsync_BaslangicTarixiSondanBoyukdurse_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.GenerateAsync(UserId,
            Request(acc.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(-10)));

        await act.Should().ThrowAsync<BadRequestException>();
    }


    /* ══════════════ ACILIS BALANSI ══════════════ */

    [Fact]
    public async Task GenerateAsync_AcilisBalansi_DovrdenEvvelkiEmeliyyatlardanHesablanir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        // dovrden EVVEL: +500, -200 → acilis = 300
        await AddTxAsync(db, In(acc.Id, 500), now.AddDays(-30));
        await AddTxAsync(db, Out(acc.Id, 200), now.AddDays(-25));
        // dovr ICINDE
        await AddTxAsync(db, In(acc.Id, 100), now.AddDays(-5));

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        result.OpeningBalance.Should().Be(300);   // 500 - 200
        result.ClosingBalance.Should().Be(400);   // 300 + 100
    }

    [Fact]
    public async Task GenerateAsync_DovrdenEvvelEmeliyyatYoxdursa_AcilisSifirdir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await AddTxAsync(db, In(acc.Id, 250), now.AddDays(-3));

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        result.OpeningBalance.Should().Be(0);
        result.ClosingBalance.Should().Be(250);
    }


    /* ══════════════ DOVR FILTRI ══════════════ */

    [Fact]
    public async Task GenerateAsync_DovrdenKenarEmeliyyatlar_SetirlereDusmur()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await AddTxAsync(db, In(acc.Id, 100), now.AddDays(-30));   // evvel
        await AddTxAsync(db, In(acc.Id, 200), now.AddDays(-5));    // ICINDE
        await AddTxAsync(db, In(acc.Id, 300), now.AddDays(5));     // sonra

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        result.TransactionCount.Should().Be(1);
        result.Lines.Should().HaveCount(1);
        result.Lines[0].Amount.Should().Be(200);
    }

    [Fact]
    public async Task GenerateAsync_BasqaHesabinEmeliyyatlari_DaxilEdilmir()
    {
        using var db = TestDb.Create();
        var mine = Account();
        var other = Account();
        db.Accounts.AddRange(mine, other);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await AddTxAsync(db, In(mine.Id, 100), now.AddDays(-3));
        await AddTxAsync(db, In(other.Id, 999), now.AddDays(-3));   // basqa hesab

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(mine.Id, now.AddDays(-10), now));

        result.TransactionCount.Should().Be(1);
        result.Lines[0].Amount.Should().Be(100);
    }

    [Fact]
    public async Task GenerateAsync_TamamlanmamisEmeliyyatlar_DaxilEdilmir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;

        var pending = In(acc.Id, 500);
        pending.Status = TransactionStatus.Pending;   // hele tamamlanmayib
        await AddTxAsync(db, pending, now.AddDays(-3));

        await AddTxAsync(db, In(acc.Id, 100), now.AddDays(-2));

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        // yalniz Completed emeliyyatlar cixarisa dusur
        result.TransactionCount.Should().Be(1);
        result.TotalIn.Should().Be(100);
    }


    /* ══════════════ CEMLER VE ISLEK BALANS ══════════════ */

    [Fact]
    public async Task GenerateAsync_CemleriDuzgunHesablayir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await AddTxAsync(db, In(acc.Id, 500), now.AddDays(-5));
        await AddTxAsync(db, In(acc.Id, 300), now.AddDays(-4));
        await AddTxAsync(db, Out(acc.Id, 200), now.AddDays(-3));

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        result.TotalIn.Should().Be(800);    // 500 + 300
        result.TotalOut.Should().Be(200);
        result.TransactionCount.Should().Be(3);
    }

    [Fact]
    public async Task GenerateAsync_IslekBalans_HerSetirdeDogruYigilir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await AddTxAsync(db, In(acc.Id, 1000), now.AddDays(-20));   // acilis: 1000
        await AddTxAsync(db, In(acc.Id, 500), now.AddDays(-5));
        await AddTxAsync(db, Out(acc.Id, 200), now.AddDays(-4));
        await AddTxAsync(db, Out(acc.Id, 100), now.AddDays(-3));

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        result.OpeningBalance.Should().Be(1000);

        // her setirde qaci balans dogru olmalidir
        result.Lines[0].BalanceAfter.Should().Be(1500);   // 1000 + 500
        result.Lines[1].BalanceAfter.Should().Be(1300);   // 1500 - 200
        result.Lines[2].BalanceAfter.Should().Be(1200);   // 1300 - 100

        result.ClosingBalance.Should().Be(1200);
    }

    [Fact]
    public async Task GenerateAsync_SetirlerTarixSirasiIleDuzulur()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        // QARISIQ sira ile elave edirik — servis ozu duzmelidir
        await AddTxAsync(db, In(acc.Id, 300), now.AddDays(-2));
        await AddTxAsync(db, In(acc.Id, 100), now.AddDays(-8));
        await AddTxAsync(db, In(acc.Id, 200), now.AddDays(-5));

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        // cixaris HEMISE kohneden yeniye getmelidir
        result.Lines.Select(l => l.Amount).Should().ContainInOrder(100m, 200m, 300m);
        result.Lines.Should().BeInAscendingOrder(l => l.Date);
    }

    [Fact]
    public async Task GenerateAsync_Istiqamet_DuzgunIsarelenir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await AddTxAsync(db, In(acc.Id, 100), now.AddDays(-5));
        await AddTxAsync(db, Out(acc.Id, 50), now.AddDays(-4));

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        result.Lines[0].Direction.Should().Be("In");
        result.Lines[1].Direction.Should().Be("Out");
    }

    [Fact]
    public async Task GenerateAsync_BosDovr_SifirlaQayidir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        await AddTxAsync(db, In(acc.Id, 500), now.AddDays(-30));   // dovrden evvel

        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId, Request(acc.Id, now.AddDays(-10), now));

        result.Lines.Should().BeEmpty();
        result.TransactionCount.Should().Be(0);
        result.TotalIn.Should().Be(0);
        result.TotalOut.Should().Be(0);
        // emeliyyat olmasa acilis = baglanis
        result.OpeningBalance.Should().Be(500);
        result.ClosingBalance.Should().Be(500);
    }

    [Fact]
    public async Task GenerateAsync_HesabNomresiCixarisaDuser()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.GenerateAsync(UserId,
            Request(acc.Id, DateTime.UtcNow.AddDays(-10), DateTime.UtcNow));

        result.AccountNumber.Should().Be("AZ0000000000000001");
    }
}