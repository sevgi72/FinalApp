using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Transaction;
using BankApiApp.DTOs.Transactions;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// Pul herekati ile bagli en kritik servis.
/// Burada sehv = musterinin pulunun itmesi, ona gore serhed hallari test olunur:
/// menfi mebleg, kifayet etmeyen balans, dondurulmus hesab, oz-ozune kocurme.
/// </summary>
public class TransactionServiceTests
{
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<ILimitService> _limitService = new();
    private readonly Mock<IRoundUpService> _roundUpService = new();
    private readonly Mock<ILocalizationService> _localizer = new();
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IBalanceAlertService> _balanceAlertService = new();
    private readonly Mock<ICommissionService> _commissionService = new();

    public TransactionServiceTests()
    {
        // localizer acari oldugu kimi qaytarsin — testlerde mesaj metni vacib deyil
        _localizer.Setup(x => x.Get(It.IsAny<string>())).Returns((string key) => key);

        // mapper: sade DTO qaytarsin (testler DTO-ni yox, BAZANI yoxlayir)
        _mapper.Setup(x => x.Map<TransactionDto>(It.IsAny<Transaction>()))
               .Returns((Transaction t) => new TransactionDto { Amount = t.Amount });

        // default: komissiya yoxdur (lazim olan testde ayrica teyin edilir)
        _commissionService.Setup(x => x.CalculateAsync(It.IsAny<string>(), It.IsAny<decimal>()))
                          .ReturnsAsync(0m);
    }

    private TransactionService CreateSut(AppDbContext db) => new(
        db, _mapper.Object, _limitService.Object, _roundUpService.Object,
        _localizer.Object, _notificationService.Object,
        _balanceAlertService.Object, _commissionService.Object);

    private static Account Account(
        string ownerId = UserId,
        decimal balance = 1000,
        string number = "AZ0000000000000001",
        AccountStatus status = AccountStatus.Active) => new()
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Balance = balance,
            AccountNumber = number,
            Status = status
        };


    /* ══════════════ DEPOSIT ══════════════ */

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public async Task DepositAsync_MeblegMusbetDeyilse_XetaAtir(decimal amount)
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.DepositAsync(UserId, new DepositDto { AccountId = acc.Id, Amount = amount });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task DepositAsync_DuzgunMebleg_BalansiArtirirVeSnapshotYazir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 1000);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.DepositAsync(UserId, new DepositDto { AccountId = acc.Id, Amount = 250 });

        acc.Balance.Should().Be(1250);

        // F4 telebi: emeliyyatdan sonraki balans qeyd olunmalidir
        var trx = db.Transactions.Single();
        trx.ToBalanceSnapshot.Should().Be(1250);
        trx.Type.Should().Be(TransactionType.Deposit);
    }

    [Fact]
    public async Task DepositAsync_BasqasininHesabi_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(ownerId: OtherUserId);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // TEHLUKESIZLIK: basqasinin hesabina giris olmamalidir
        var act = async () => await sut.DepositAsync(UserId, new DepositDto { AccountId = acc.Id, Amount = 100 });

        await act.Should().ThrowAsync<NotFoundException>();
    }


    /* ══════════════ WITHDRAW ══════════════ */

    [Fact]
    public async Task WithdrawAsync_BalansKifayetEtmirse_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.WithdrawAsync(UserId, new WithdrawDto { AccountId = acc.Id, Amount = 100.01m });

        await act.Should().ThrowAsync<BadRequestException>();
        acc.Balance.Should().Be(100);   // balans deyismemelidir
    }

    [Fact]
    public async Task WithdrawAsync_BalansaBeraberMebleg_IcazeVerilir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // serhed hali: tam balans qeder cixaris MUMKUNDUR
        await sut.WithdrawAsync(UserId, new WithdrawDto { AccountId = acc.Id, Amount = 100 });

        acc.Balance.Should().Be(0);
    }

    [Fact]
    public async Task WithdrawAsync_Ugurlu_BalansXeberdarligiYoxlanilir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 1000);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.WithdrawAsync(UserId, new WithdrawDto { AccountId = acc.Id, Amount = 300 });

        // F7: asagi balans xeberdarligi ONCEKI balansla cagirilmalidir
        _balanceAlertService.Verify(x => x.CheckAndAlertAsync(acc, 1000m), Times.Once);
    }

    [Fact]
    public async Task WithdrawAsync_Ugurlu_LimitYoxlamasiCagirilir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 1000);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.WithdrawAsync(UserId, new WithdrawDto { AccountId = acc.Id, Amount = 300 });

        // gunluk/ayliq limit yoxlanmalidir
        _limitService.Verify(x => x.EnsureWithinLimitsAsync(acc, 300m), Times.Once);
    }


    /* ══════════════ TRANSFER ══════════════ */

    [Fact]
    public async Task TransferAsync_AlanHesabTapilmirsa_XetaAtir()
    {
        using var db = TestDb.Create();
        var from = Account();
        db.Accounts.Add(from);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.TransferAsync(UserId, new TransferDto
        {
            FromAccountId = from.Id,
            ToAccountNumber = "AZ9999999999999999",
            Amount = 100
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task TransferAsync_OzHesabinaKocurme_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(number: "AZ1111");
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // eyni hesaba kocurme menasizdir — bloklanmalidir
        var act = async () => await sut.TransferAsync(UserId, new TransferDto
        {
            FromAccountId = acc.Id,
            ToAccountNumber = "AZ1111",
            Amount = 100
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task TransferAsync_AlanHesabBloklanibsa_XetaAtir()
    {
        using var db = TestDb.Create();
        var from = Account(number: "AZ1111");
        var to = Account(ownerId: OtherUserId, number: "AZ2222", status: AccountStatus.Blocked);
        db.Accounts.AddRange(from, to);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.TransferAsync(UserId, new TransferDto
        {
            FromAccountId = from.Id,
            ToAccountNumber = "AZ2222",
            Amount = 100
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task TransferAsync_BalansMeblegPlusKomissiyaUcunKifayetEtmirse_XetaAtir()
    {
        using var db = TestDb.Create();
        var from = Account(balance: 100, number: "AZ1111");
        var to = Account(ownerId: OtherUserId, number: "AZ2222", balance: 0);
        db.Accounts.AddRange(from, to);
        await db.SaveChangesAsync();

        // komissiya 5 AZN — yeni cemi 105 lazimdir, balans ise 100
        _commissionService.Setup(x => x.CalculateAsync("Transfer", 100m)).ReturnsAsync(5m);
        var sut = CreateSut(db);

        var act = async () => await sut.TransferAsync(UserId, new TransferDto
        {
            FromAccountId = from.Id,
            ToAccountNumber = "AZ2222",
            Amount = 100
        });

        // KRITIK: komissiya nezere alinmasa, balans menfiye duserdi
        await act.Should().ThrowAsync<BadRequestException>();
        from.Balance.Should().Be(100);
        to.Balance.Should().Be(0);
    }

    [Fact]
    public async Task TransferAsync_Ugurlu_PulKocurulurVeKomissiyaTutulur()
    {
        using var db = TestDb.Create();
        var from = Account(balance: 1000, number: "AZ1111");
        var to = Account(ownerId: OtherUserId, number: "AZ2222", balance: 500);
        db.Accounts.AddRange(from, to);
        await db.SaveChangesAsync();

        _commissionService.Setup(x => x.CalculateAsync("Transfer", 200m)).ReturnsAsync(4m);
        var sut = CreateSut(db);

        await sut.TransferAsync(UserId, new TransferDto
        {
            FromAccountId = from.Id,
            ToAccountNumber = "AZ2222",
            Amount = 200,
            Description = "Test"
        });

        // gonderenden mebleg + komissiya cixir
        from.Balance.Should().Be(796);   // 1000 - (200 + 4)
        // alan YALNIZ esas meblegi alir (komissiya banka qalir)
        to.Balance.Should().Be(700);     // 500 + 200

        // iki qeyd yaranmalidir: kocurme + komissiya
        db.Transactions.Should().HaveCount(2);
        db.Transactions.Count(t => t.Description == "Kocurme komissiyasi").Should().Be(1);
    }

    [Fact]
    public async Task TransferAsync_KomissiyaSifirdirsa_AyricaQeydYaradilmir()
    {
        using var db = TestDb.Create();
        var from = Account(balance: 1000, number: "AZ1111");
        var to = Account(ownerId: OtherUserId, number: "AZ2222", balance: 0);
        db.Accounts.AddRange(from, to);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);   // default komissiya = 0

        await sut.TransferAsync(UserId, new TransferDto
        {
            FromAccountId = from.Id,
            ToAccountNumber = "AZ2222",
            Amount = 200
        });

        // komissiya yoxdursa — artiq qeyd olmamalidir
        db.Transactions.Should().HaveCount(1);
        from.Balance.Should().Be(800);
    }


    /* ══════════════ SELF TRANSFER ══════════════ */

    [Fact]
    public async Task SelfTransferAsync_EyniHesab_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.SelfTransferAsync(UserId, new SelfTransferDto
        {
            FromAccountId = acc.Id,
            ToAccountId = acc.Id,
            Amount = 100
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task SelfTransferAsync_HedefHesabBasqasinindirsa_XetaAtir()
    {
        using var db = TestDb.Create();
        var mine = Account(number: "AZ1111");
        var foreign = Account(ownerId: OtherUserId, number: "AZ2222");
        db.Accounts.AddRange(mine, foreign);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // TEHLUKESIZLIK: "oz hesablari arasi" kocurme yalniz OZ hesablari ucundur
        var act = async () => await sut.SelfTransferAsync(UserId, new SelfTransferDto
        {
            FromAccountId = mine.Id,
            ToAccountId = foreign.Id,
            Amount = 100
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SelfTransferAsync_Ugurlu_PulKocurulurKomissiyasiz()
    {
        using var db = TestDb.Create();
        var a = Account(balance: 1000, number: "AZ1111");
        var b = Account(balance: 200, number: "AZ2222");
        db.Accounts.AddRange(a, b);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.SelfTransferAsync(UserId, new SelfTransferDto
        {
            FromAccountId = a.Id,
            ToAccountId = b.Id,
            Amount = 300
        });

        // oz pulu — komissiya tutulmur, cemi deyismir
        a.Balance.Should().Be(700);
        b.Balance.Should().Be(500);
        (a.Balance + b.Balance).Should().Be(1200);
    }

    [Fact]
    public async Task SelfTransferAsync_LimitYoxlamasiCagirilmir()
    {
        using var db = TestDb.Create();
        var a = Account(balance: 1000, number: "AZ1111");
        var b = Account(balance: 0, number: "AZ2222");
        db.Accounts.AddRange(a, b);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.SelfTransferAsync(UserId, new SelfTransferDto
        {
            FromAccountId = a.Id,
            ToAccountId = b.Id,
            Amount = 900
        });

        // oz hesablari arasi kocurmede limit TETBIQ OLUNMUR (dizayn qerari)
        _limitService.Verify(x => x.EnsureWithinLimitsAsync(It.IsAny<Account>(), It.IsAny<decimal>()), Times.Never);
    }
}