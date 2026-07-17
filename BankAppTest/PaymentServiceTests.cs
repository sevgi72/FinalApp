using BankApiApp.Data;
using BankApiApp.DTOs.Payment;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// Kartla balans artirma — Luhn yoxlamasi, kart validasiyasi ve ugursuz
/// odenislerin loglanmasi.
/// QEYD: servis SimulateBankApproval() ucun Random.Shared isledir, yeni ugur
/// yolu determinist deyil. Validasiya yollari ise tam determinist-dir
/// (random-dan EVVEL qayidir) — testler buna gore qurulub.
/// </summary>
public class PaymentServiceTests
{
    private const string UserId = "user-1";

    // Luhn yoxlamasindan KECEN nomre (klassik test karti)
    private const string ValidCard = "4111111111111111";
    // eyni nomre, yalniz son reqem deyisdirilib — Luhn-dan KECMIR
    private const string LuhnFailCard = "4111111111111112";

    private readonly Mock<ILocalizationService> _localizer = new();

    public PaymentServiceTests()
    {
        _localizer.Setup(x => x.Get(It.IsAny<string>())).Returns((string key) => key);
    }

    private PaymentService CreateSut(AppDbContext db) => new(db, _localizer.Object);

    private static Account Account(decimal balance = 100, string ownerId = UserId) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        Balance = balance,
        AccountNumber = "AZ0000000000000001",
        Status = AccountStatus.Active
    };

    // gelecek tarixli, kecerli kart
    private static TopUpDto TopUp(Guid accountId, string card = ValidCard,
        decimal amount = 50, int? month = null, int? year = null, string cvv = "123") => new()
        {
            AccountId = accountId,
            CardNumber = card,
            Amount = amount,
            ExpiryMonth = month ?? 12,
            ExpiryYear = year ?? DateTime.UtcNow.Year + 2,
            Cvv = cvv
        };


    /* ══════════════ HESAB YOXLAMALARI ══════════════ */

    [Fact]
    public async Task TopUpAsync_HesabTapilmirsa_XetaAtir()
    {
        using var db = TestDb.Create();
        var sut = CreateSut(db);

        var act = async () => await sut.TopUpAsync(UserId, TopUp(Guid.NewGuid()));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task TopUpAsync_BasqasininHesabi_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(ownerId: "basqa-user");
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // TEHLUKESIZLIK: basqasinin hesabini doldurmaq olmaz
        var act = async () => await sut.TopUpAsync(UserId, TopUp(acc.Id));

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public async Task TopUpAsync_MeblegMusbetDeyilse_XetaAtir(decimal amount)
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.TopUpAsync(UserId, TopUp(acc.Id, amount: amount));

        await act.Should().ThrowAsync<BadRequestException>();
    }


    /* ══════════════ LUHN ALQORITMI ══════════════ */

    [Fact]
    public async Task TopUpAsync_LuhndanKecmeyenKart_UgursuzOlur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.TopUpAsync(UserId, TopUp(acc.Id, card: LuhnFailCard));

        result.Success.Should().BeFalse();
        acc.Balance.Should().Be(100);   // balans deyismir
    }

    [Fact]
    public async Task TopUpAsync_LuhndanKecenKart_ValidasiyaniAsir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.TopUpAsync(UserId, TopUp(acc.Id, card: ValidCard));

        // kecerli kart bank simulyasiyasina qeder gelir —
        // ya ugurlu olur, ya da bank terefinden redd edilir (Random).
        // Yeni: "kart nomresi etibarsizdir" mesaji GELMEMELIDIR.
        result.Message.Should().NotContain("Luhn");
    }


    /* ══════════════ KART FORMATI ══════════════ */

    [Theory]
    [InlineData("411111111111")]          // 12 reqem — qisadir
    [InlineData("41111111111111111")]     // 17 reqem — uzundur
    [InlineData("4111abcd11111111")]      // herf var
    [InlineData("")]                      // bos
    public async Task TopUpAsync_KartNomresiFormatiYanlis_UgursuzOlur(string card)
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.TopUpAsync(UserId, TopUp(acc.Id, card: card));

        result.Success.Should().BeFalse();
        acc.Balance.Should().Be(100);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(-1)]
    public async Task TopUpAsync_BitmeAyiYanlis_UgursuzOlur(int month)
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.TopUpAsync(UserId, TopUp(acc.Id, month: month));

        result.Success.Should().BeFalse();
    }

    [Theory]
    [InlineData("12")]      // 2 reqem
    [InlineData("1234")]    // 4 reqem
    [InlineData("abc")]     // herf
    [InlineData("")]        // bos
    public async Task TopUpAsync_CvvYanlis_UgursuzOlur(string cvv)
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.TopUpAsync(UserId, TopUp(acc.Id, cvv: cvv));

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task TopUpAsync_KartinVaxtiKecib_UgursuzOlur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // 2020-ci ilin yanvari — coxdan kecib
        var result = await sut.TopUpAsync(UserId, TopUp(acc.Id, month: 1, year: 2020));

        result.Success.Should().BeFalse();
        acc.Balance.Should().Be(100);
    }


    /* ══════════════ UGURSUZ ODENISIN LOGLANMASI ══════════════ */

    [Fact]
    public async Task TopUpAsync_Ugursuz_OdenisFailedKimiQeydeAlinir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var result = await sut.TopUpAsync(UserId, TopUp(acc.Id, card: LuhnFailCard));

        // AUDIT: ugursuz cehd de qeyd olunmalidir (fraud analizi ucun)
        var payment = db.Payments.Single();
        payment.Status.Should().Be(PaymentStatus.Failed);
        payment.FailureReason.Should().NotBeNullOrEmpty();
        payment.CardLast4.Should().Be("1112");        // yalniz son 4 reqem saxlanilir
        payment.Reference.Should().Be(result.Reference);

        // TAM KART NOMRESI HEC VAXT SAXLANMAMALIDIR (PCI DSS prinsipi)
        payment.CardLast4.Length.Should().Be(4);
    }

    [Fact]
    public async Task TopUpAsync_Ugursuz_EmeliyyatTarixcesineDusmur()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.TopUpAsync(UserId, TopUp(acc.Id, card: LuhnFailCard));

        // ugursuz odenis Payment-de var, amma Transaction yaratmamalidir
        db.Transactions.Should().BeEmpty();
    }


    /* ══════════════ UGURLU ODENIS ══════════════ */

    [Fact]
    public async Task TopUpAsync_KecerliKart_BalansiArtirir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 100);
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // Servis bank cavabini Random ile simulyasiya edir (~10% redd).
        // Determinist netice ucun ugur alinana qeder cehd edirik —
        // 30 cehdin hamisinin ugursuz olma ehtimali ~10^-30.
        PaymentResultDto? success = null;
        for (int i = 0; i < 30 && success == null; i++)
        {
            var r = await sut.TopUpAsync(UserId, TopUp(acc.Id, amount: 50));
            if (r.Success) success = r;
        }

        success.Should().NotBeNull("kecerli kart eninde-sonunda qebul edilmelidir");
        success!.Status.Should().Be(PaymentStatus.Completed.ToString());
        success.Reference.Should().StartWith("PAY-");

        // ugurlu odenis Deposit emeliyyati yaratmalidir
        db.Transactions.Should().Contain(t => t.Type == TransactionType.Deposit
                                           && t.ToAccountId == acc.Id);
    }

    [Fact]
    public async Task TopUpAsync_KartNomresindekiBosluqlar_Temizlenir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // istifadeci "4111 1111 1111 1111" yazir — bosluqlar problem olmamalidir
        var result = await sut.TopUpAsync(UserId, TopUp(acc.Id, card: "4111 1111 1111 1111"));

        // format xetasi ALINMAMALIDIR (bosluqlar silinir, 16 reqem qalir)
        db.Payments.Single().CardLast4.Should().Be("1111");
    }
}