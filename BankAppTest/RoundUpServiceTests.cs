using BankApiApp.DTOs.RoundUp;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// Round-up — xercin qepiklerini yuvarlaqlasdirib yigim hesabina kocurur.
/// Esas dizayn prinsipi: round-up HEC VAXT esas emeliyyati poza bilmez —
/// problem olanda sessizce atlanir.
/// </summary>
public class RoundUpServiceTests
{
    private const string UserId = "user-1";

    private readonly Mock<ILocalizationService> _localizer = new();

    public RoundUpServiceTests()
    {
        _localizer.Setup(x => x.Get(It.IsAny<string>())).Returns((string key) => key);
    }

    private static Account Account(decimal balance = 1000, string ownerId = UserId,
        AccountStatus status = AccountStatus.Active) => new()
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Balance = balance,
            AccountNumber = "AZ0000000000000001",
            Status = status
        };

    private static RoundUpSetting Setting(Guid sourceId, Guid targetId,
        int roundTo = 1, bool enabled = true) => new()
        {
            UserId = UserId,
            SourceAccountId = sourceId,
            TargetAccountId = targetId,
            RoundTo = roundTo,
            IsEnabled = enabled
        };


    /* ══════════════ YUVARLAQLASDIRMA RIYAZIYYATI ══════════════ */

    [Theory]
    [InlineData(4.30, 1, 0.70)]     // 4.30 → 5.00, ferq 0.70
    [InlineData(12.01, 1, 0.99)]    // 12.01 → 13.00, ferq 0.99
    [InlineData(0.10, 1, 0.90)]     // kicik xerc
    [InlineData(12.00, 5, 3.00)]    // 12 → 15 (5-e yuvarlaqlasdirma)
    [InlineData(4.30, 5, 0.70)]     // 4.30 → 5.00
    [InlineData(7.50, 5, 2.50)]     // 7.50 → 10.00
    public async Task TryProcessAsync_FerqiDuzgunHesablayir(decimal spent, int roundTo, decimal expected)
    {
        using var db = TestDb.Create();
        var source = Account(balance: 1000);
        var target = Account(balance: 0);
        db.Accounts.AddRange(source, target);
        db.RoundUpSettings.Add(Setting(source.Id, target.Id, roundTo));
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.TryProcessAsync(source, spent);

        target.Balance.Should().Be(expected);
        source.Balance.Should().Be(1000 - expected);
    }

    [Theory]
    [InlineData(5.00, 1)]    // tam eded — yuvarlaqlasdirilasi ferq yoxdur
    [InlineData(10.00, 5)]   // 10 onsuz 5-in misli
    public async Task TryProcessAsync_MeblegOnsuzTamdirsa_YigimOlmur(decimal spent, int roundTo)
    {
        using var db = TestDb.Create();
        var source = Account(balance: 1000);
        var target = Account(balance: 0);
        db.Accounts.AddRange(source, target);
        db.RoundUpSettings.Add(Setting(source.Id, target.Id, roundTo));
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.TryProcessAsync(source, spent);

        target.Balance.Should().Be(0);
        source.Balance.Should().Be(1000);
        db.Transactions.Should().BeEmpty();
    }


    /* ══════════════ ATLAMA SERTLERI ══════════════ */

    [Fact]
    public async Task TryProcessAsync_AyarYoxdursa_SessizceAtlanir()
    {
        using var db = TestDb.Create();
        var source = Account(balance: 1000);
        db.Accounts.Add(source);
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.TryProcessAsync(source, 4.30m);

        source.Balance.Should().Be(1000);
        db.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task TryProcessAsync_AyarSondurulubse_Atlanir()
    {
        using var db = TestDb.Create();
        var source = Account(balance: 1000);
        var target = Account(balance: 0);
        db.Accounts.AddRange(source, target);
        db.RoundUpSettings.Add(Setting(source.Id, target.Id, enabled: false));
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.TryProcessAsync(source, 4.30m);

        target.Balance.Should().Be(0);
        source.Balance.Should().Be(1000);
    }

    [Fact]
    public async Task TryProcessAsync_QaliqBalansFerqeCatmirsa_Atlanir()
    {
        using var db = TestDb.Create();
        // xercden sonra hesabda cemi 0.50 qalib, ferq ise 0.70-dir
        var source = Account(balance: 0.50m);
        var target = Account(balance: 0);
        db.Accounts.AddRange(source, target);
        db.RoundUpSettings.Add(Setting(source.Id, target.Id));
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.TryProcessAsync(source, 4.30m);

        // DIZAYN: yigim ucun pul catmirsa — balans MENFIYE dusmemelidir
        source.Balance.Should().Be(0.50m);
        target.Balance.Should().Be(0);
    }

    [Fact]
    public async Task TryProcessAsync_YigimHesabiDondurulubsa_Atlanir()
    {
        using var db = TestDb.Create();
        var source = Account(balance: 1000);
        var target = Account(balance: 0, status: AccountStatus.Blocked);
        db.Accounts.AddRange(source, target);
        db.RoundUpSettings.Add(Setting(source.Id, target.Id));
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.TryProcessAsync(source, 4.30m);

        source.Balance.Should().Be(1000);
        target.Balance.Should().Be(0);
    }

    [Fact]
    public async Task TryProcessAsync_Ugurlu_TarixceyeIsareliQeydDuser()
    {
        using var db = TestDb.Create();
        var source = Account(balance: 1000);
        var target = Account(balance: 0);
        db.Accounts.AddRange(source, target);
        db.RoundUpSettings.Add(Setting(source.Id, target.Id));
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.TryProcessAsync(source, 4.30m);

        // qeyd tanina bilen tesvirle yaranmalidir (statistika buna gore hesablanir)
        var trx = db.Transactions.Single();
        trx.Description.Should().Be(RoundUpService.RoundUpDescription);
        trx.Amount.Should().Be(0.70m);
        trx.FromAccountId.Should().Be(source.Id);
        trx.ToAccountId.Should().Be(target.Id);
    }


    /* ══════════════ AKTIVLESDIRME ══════════════ */

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(10)]
    public async Task EnableAsync_YuvarlaqlasdirmaYalniz1Ve5OlaBiler(int roundTo)
    {
        using var db = TestDb.Create();
        var source = Account();
        var target = Account();
        db.Accounts.AddRange(source, target);
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        var act = async () => await sut.EnableAsync(UserId, new EnableRoundUpDto
        {
            SourceAccountId = source.Id,
            TargetAccountId = target.Id,
            RoundTo = roundTo
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task EnableAsync_MenbeVeHedefEynidirse_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        // eyni hesaba yigim menasizdir
        var act = async () => await sut.EnableAsync(UserId, new EnableRoundUpDto
        {
            SourceAccountId = acc.Id,
            TargetAccountId = acc.Id,
            RoundTo = 1
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task EnableAsync_HedefHesabBasqasinindirsa_XetaAtir()
    {
        using var db = TestDb.Create();
        var source = Account();
        var foreign = Account(ownerId: "basqa-user");
        db.Accounts.AddRange(source, foreign);
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        // TEHLUKESIZLIK: basqasinin hesabina yigim yonlendirile bilmez
        var act = async () => await sut.EnableAsync(UserId, new EnableRoundUpDto
        {
            SourceAccountId = source.Id,
            TargetAccountId = foreign.Id,
            RoundTo = 1
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task EnableAsync_Ugurlu_AyarYaradilir()
    {
        using var db = TestDb.Create();
        var source = Account();
        var target = Account();
        db.Accounts.AddRange(source, target);
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.EnableAsync(UserId, new EnableRoundUpDto
        {
            SourceAccountId = source.Id,
            TargetAccountId = target.Id,
            RoundTo = 5
        });

        var setting = db.RoundUpSettings.Single();
        setting.IsEnabled.Should().BeTrue();
        setting.RoundTo.Should().Be(5);
        setting.SourceAccountId.Should().Be(source.Id);
    }

    [Fact]
    public async Task EnableAsync_AyarVarsa_YenisiYaradilmirYenilenir()
    {
        using var db = TestDb.Create();
        var source = Account();
        var target = Account();
        db.Accounts.AddRange(source, target);
        db.RoundUpSettings.Add(Setting(source.Id, target.Id, roundTo: 1, enabled: false));
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.EnableAsync(UserId, new EnableRoundUpDto
        {
            SourceAccountId = source.Id,
            TargetAccountId = target.Id,
            RoundTo = 5
        });

        // dublikat ayar yaranmamalidir
        db.RoundUpSettings.Should().HaveCount(1);
        var setting = db.RoundUpSettings.Single();
        setting.RoundTo.Should().Be(5);
        setting.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DisableAsync_AyarYoxdursa_XetaAtir()
    {
        using var db = TestDb.Create();
        var sut = new RoundUpService(db, _localizer.Object);

        var act = async () => await sut.DisableAsync(UserId);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DisableAsync_Ugurlu_AyarSondurulurAmmaSilinmir()
    {
        using var db = TestDb.Create();
        var source = Account();
        var target = Account();
        db.Accounts.AddRange(source, target);
        db.RoundUpSettings.Add(Setting(source.Id, target.Id));
        await db.SaveChangesAsync();
        var sut = new RoundUpService(db, _localizer.Object);

        await sut.DisableAsync(UserId);

        // ayar saxlanilir (istifadeci yeniden acsa, secimleri qalir)
        var setting = db.RoundUpSettings.Single();
        setting.IsEnabled.Should().BeFalse();
        setting.SourceAccountId.Should().Be(source.Id);
    }
}