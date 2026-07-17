using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// F7 — asagi balans xeberdarligi.
/// EN VACIB QAYDA: bildiris yalniz hedden YENICE kecende gonderilir
/// (wasAbove && nowBelow). Balans onsuz hedden asagi idise, her emeliyyatda
/// tekrar bildiris getmemelidir — yoxsa istifadeci spam alar.
/// </summary>
public class BalanceAlertServiceTests
{
    private const string UserId = "user-1";

    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<ILocalizationService> _localizer = new();

    public BalanceAlertServiceTests()
    {
        _localizer.Setup(x => x.Get(It.IsAny<string>())).Returns((string key) => key);
    }

    private static Account Account(decimal balance, decimal? threshold) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = UserId,
        Balance = balance,
        AccountNumber = "AZ0000000000000001",
        Status = AccountStatus.Active,
        LowBalanceThreshold = threshold
    };

    // bildiris gonderildi?
    private void VerifyAlert(Times times) =>
        _notificationService.Verify(x => x.SendToUserAsync(
            UserId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), times);


    /* ══════════════ CHECK & ALERT — esas qayda ══════════════ */

    [Fact]
    public async Task CheckAndAlertAsync_HeddenYeniceKecende_BildirisGonderilir()
    {
        using var db = TestDb.Create();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        // hedd 100: evvel 500 idi (yuxari), indi 80 (asagi) → KECID bas verdi
        var account = Account(balance: 80, threshold: 100);

        await sut.CheckAndAlertAsync(account, balanceBefore: 500);

        VerifyAlert(Times.Once());
    }

    [Fact]
    public async Task CheckAndAlertAsync_BalansOnsuzHeddenAsagiIdise_BildirisGetmir()
    {
        using var db = TestDb.Create();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        // hedd 100: evvel 90 (ONSUZ asagi), indi 50 → kecid YOXDUR
        // Bu, spam-in qarsisini alir: istifadeci artiq xeberdar edilib.
        var account = Account(balance: 50, threshold: 100);

        await sut.CheckAndAlertAsync(account, balanceBefore: 90);

        VerifyAlert(Times.Never());
    }

    [Fact]
    public async Task CheckAndAlertAsync_BalansHeleHeddenYuxaridirsa_BildirisGetmir()
    {
        using var db = TestDb.Create();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        // hedd 100: 500 → 300, hele de yuxari
        var account = Account(balance: 300, threshold: 100);

        await sut.CheckAndAlertAsync(account, balanceBefore: 500);

        VerifyAlert(Times.Never());
    }

    [Fact]
    public async Task CheckAndAlertAsync_BalansTamHedde_BildirisGetmir()
    {
        using var db = TestDb.Create();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        // SERHED HALI: hedd 100, balans tam 100.
        // Qayda "< threshold"-dur, yeni tam hedde bildiris YOXDUR.
        var account = Account(balance: 100, threshold: 100);

        await sut.CheckAndAlertAsync(account, balanceBefore: 500);

        VerifyAlert(Times.Never());
    }

    [Fact]
    public async Task CheckAndAlertAsync_BalansHeddenBirQepikAsagi_BildirisGonderilir()
    {
        using var db = TestDb.Create();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        // SERHED HALI: 99.99 < 100 → bildiris getmelidir
        var account = Account(balance: 99.99m, threshold: 100);

        await sut.CheckAndAlertAsync(account, balanceBefore: 100);

        VerifyAlert(Times.Once());
    }

    [Fact]
    public async Task CheckAndAlertAsync_HeddTeyinEdilmeyibse_BildirisGetmir()
    {
        using var db = TestDb.Create();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        // xeberdarliq sondurulub (threshold = null)
        var account = Account(balance: 0, threshold: null);

        await sut.CheckAndAlertAsync(account, balanceBefore: 1000);

        VerifyAlert(Times.Never());
    }

    [Fact]
    public async Task CheckAndAlertAsync_BildirisServisiXetaAtsa_EsasEmeliyyatiPozmur()
    {
        using var db = TestDb.Create();

        // bildiris servisi cokur
        _notificationService
            .Setup(x => x.SendToUserAsync(It.IsAny<string>(), It.IsAny<string>(),
                                          It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("SMTP dead"));

        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);
        var account = Account(balance: 50, threshold: 100);

        var act = async () => await sut.CheckAndAlertAsync(account, balanceBefore: 500);

        // DIZAYN QERARI: xeberdarliq ikinci dereceli isdir —
        // cokerse kocurme/cixaris ugursuz sayilmamalidir.
        await act.Should().NotThrowAsync();
    }


    /* ══════════════ SET THRESHOLD ══════════════ */

    [Fact]
    public async Task SetThresholdAsync_DuzgunDeyer_HesabaYazilir()
    {
        using var db = TestDb.Create();
        var account = Account(balance: 1000, threshold: null);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        await sut.SetThresholdAsync(UserId, account.Id, 250);

        account.LowBalanceThreshold.Should().Be(250);
    }

    [Fact]
    public async Task SetThresholdAsync_NullVerilse_XeberdarliqLegvOlunur()
    {
        using var db = TestDb.Create();
        var account = Account(balance: 1000, threshold: 250);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        await sut.SetThresholdAsync(UserId, account.Id, null);

        account.LowBalanceThreshold.Should().BeNull();
    }

    [Fact]
    public async Task SetThresholdAsync_MenfiHedd_XetaAtir()
    {
        using var db = TestDb.Create();
        var account = Account(balance: 1000, threshold: null);
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        var act = async () => await sut.SetThresholdAsync(UserId, account.Id, -1);

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task SetThresholdAsync_BasqasininHesabi_XetaAtir()
    {
        using var db = TestDb.Create();
        var account = Account(balance: 1000, threshold: null);
        account.OwnerId = "basqa-user";
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        var sut = new BalanceAlertService(db, _notificationService.Object, _localizer.Object);

        // TEHLUKESIZLIK: basqasinin hesabina hedd qoyula bilmez
        var act = async () => await sut.SetThresholdAsync(UserId, account.Id, 100);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}