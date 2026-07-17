using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Loan;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using BankApiApp.Services;
using BankAppTest.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BankAppTest.Services;

/// <summary>
/// F8 — kredit muracieti ve admin tesdiqi axini.
/// Yoxlanilir: amortizasiya hesablamasi, status kecidleri (Pending → Active/Rejected),
/// tesdiq zamani pulun hesaba kecmesi ve odenis cedvelinin qurulmasi.
/// </summary>
public class LoanServiceTests
{
    private const string UserId = "user-1";
    private const decimal AnnualRate = 12m;   // servisdeki sabit ile eyni

    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<ILocalizationService> _localizer = new();

    public LoanServiceTests()
    {
        _localizer.Setup(x => x.Get(It.IsAny<string>())).Returns((string key) => key);
        _mapper.Setup(x => x.Map<LoanDto>(It.IsAny<Loan>()))
               .Returns((Loan l) => new LoanDto { Principal = l.Principal });
    }

    private LoanService CreateSut(AppDbContext db) =>
        new(db, _mapper.Object, _localizer.Object);

    private static Account Account(decimal balance = 1000, string ownerId = UserId) => new()
    {
        Id = Guid.NewGuid(),
        OwnerId = ownerId,
        Balance = balance,
        AccountNumber = "AZ0000000000000001",
        Status = AccountStatus.Active
    };

    private static Loan Loan(Guid accountId, LoanStatus status = LoanStatus.Pending,
        decimal principal = 1200, int term = 12) => new()
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Principal = principal,
            InterestRate = AnnualRate,
            TermMonths = term,
            MonthlyPayment = 112,
            TotalPayable = 1344,
            RemainingBalance = 1344,
            Status = status
        };


    /* ══════════════ MURACIET ══════════════ */

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task RequestLoanAsync_MeblegMusbetDeyilse_XetaAtir(decimal principal)
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.RequestLoanAsync(UserId, new LoanRequestDto
        {
            AccountId = acc.Id,
            Principal = principal,
            TermMonths = 12
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-6)]
    public async Task RequestLoanAsync_MuddetMusbetDeyilse_XetaAtir(int term)
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.RequestLoanAsync(UserId, new LoanRequestDto
        {
            AccountId = acc.Id,
            Principal = 1000,
            TermMonths = term
        });

        await act.Should().ThrowAsync<BadRequestException>();
    }

    [Fact]
    public async Task RequestLoanAsync_BasqasininHesabi_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account(ownerId: "basqa-user");
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        var act = async () => await sut.RequestLoanAsync(UserId, new LoanRequestDto
        {
            AccountId = acc.Id,
            Principal = 1000,
            TermMonths = 12
        });

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RequestLoanAsync_Ugurlu_PendingStatuslaYaradilir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.RequestLoanAsync(UserId, new LoanRequestDto
        {
            AccountId = acc.Id,
            Principal = 1200,
            TermMonths = 12
        });

        var loan = db.Loans.Single();
        loan.Status.Should().Be(LoanStatus.Pending);   // admin tesdiqi gozleyir
        loan.InterestRate.Should().Be(AnnualRate);

        // MURACIET ANINDA PUL VERILMIR — yalniz tesdiqden sonra
        acc.Balance.Should().Be(1000);
        db.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestLoanAsync_AmortizasiyaniDuzgunHesablayir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // 1200 AZN, 12% illik, 12 ay:
        // faiz = 1200 * 0.12 * 1 il = 144
        // cemi = 1344, ayliq = 1344 / 12 = 112
        await sut.RequestLoanAsync(UserId, new LoanRequestDto
        {
            AccountId = acc.Id,
            Principal = 1200,
            TermMonths = 12
        });

        var loan = db.Loans.Single();
        loan.MonthlyPayment.Should().Be(112m);
        loan.TotalPayable.Should().Be(1344m);
        loan.RemainingBalance.Should().Be(1344m);
    }

    [Fact]
    public async Task RequestLoanAsync_Yuvarlaqlasdirmadan_CemiTutarliQalir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        db.Accounts.Add(acc);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // 1000 AZN, 12%, 6 ay: faiz = 1000*0.12*0.5 = 60, cemi = 1060
        // ayliq = round(1060/6, 2) = 176.67 → yuvarlaqlasdirma "quyruq" buraxir
        await sut.RequestLoanAsync(UserId, new LoanRequestDto
        {
            AccountId = acc.Id,
            Principal = 1000,
            TermMonths = 6
        });

        var loan = db.Loans.Single();
        loan.MonthlyPayment.Should().Be(176.67m);

        // TotalPayable ayliq odenislerin CEMINE beraber olmalidir,
        // yoxsa sonda borc tam bagalanmazdi.
        loan.TotalPayable.Should().Be(loan.MonthlyPayment * 6);
        loan.TotalPayable.Should().Be(1060.02m);
    }


    /* ══════════════ TESDIQ ══════════════ */

    [Fact]
    public async Task ApproveLoanAsync_Ugurlu_PulHesabaKecirVeCedvelQurulur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var loan = Loan(acc.Id, principal: 1200, term: 12);
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ApproveLoanAsync(loan.Id);

        loan.Status.Should().Be(LoanStatus.Active);
        loan.StartDate.Should().NotBeNull();

        // kredit meblegi hesaba kecir
        acc.Balance.Should().Be(1700);   // 500 + 1200
        db.Transactions.Should().HaveCount(1);

        // odenis cedveli: her ay ucun bir setir
        var payments = db.LoanPayments.Where(p => p.LoanId == loan.Id).ToList();
        payments.Should().HaveCount(12);
        payments.Should().OnlyContain(p => !p.IsPaid);
        payments.Select(p => p.PaymentNumber).Should().BeEquivalentTo(Enumerable.Range(1, 12));
    }

    [Fact]
    public async Task ApproveLoanAsync_OdenisTarixleriArdicilAylardir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var loan = Loan(acc.Id, term: 3);
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ApproveLoanAsync(loan.Id);

        var payments = db.LoanPayments.OrderBy(p => p.PaymentNumber).ToList();
        var start = loan.StartDate!.Value;

        payments[0].DueDate.Should().BeCloseTo(start.AddMonths(1), TimeSpan.FromSeconds(5));
        payments[1].DueDate.Should().BeCloseTo(start.AddMonths(2), TimeSpan.FromSeconds(5));
        payments[2].DueDate.Should().BeCloseTo(start.AddMonths(3), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ApproveLoanAsync_ArtiqTesdiqlenmisKredit_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var loan = Loan(acc.Id, status: LoanStatus.Active);
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // KRITIK: ikinci defe tesdiq pulu TEKRAR verirdi
        var act = async () => await sut.ApproveLoanAsync(loan.Id);

        await act.Should().ThrowAsync<BadRequestException>();
        acc.Balance.Should().Be(1000);   // pul verilmedi
    }

    [Fact]
    public async Task ApproveLoanAsync_KreditTapilmirsa_XetaAtir()
    {
        using var db = TestDb.Create();
        var sut = CreateSut(db);

        var act = async () => await sut.ApproveLoanAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>();
    }


    /* ══════════════ RED ══════════════ */

    [Fact]
    public async Task RejectLoanAsync_Ugurlu_StatusRejectedOlurPulVerilmir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var loan = Loan(acc.Id);
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.RejectLoanAsync(loan.Id);

        loan.Status.Should().Be(LoanStatus.Rejected);
        acc.Balance.Should().Be(500);
        db.Transactions.Should().BeEmpty();
    }

    [Fact]
    public async Task RejectLoanAsync_AktivKrediti_XetaAtir()
    {
        using var db = TestDb.Create();
        var acc = Account();
        var loan = Loan(acc.Id, status: LoanStatus.Active);
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        // verilmis krediti "red etmek" olmaz
        var act = async () => await sut.RejectLoanAsync(loan.Id);

        await act.Should().ThrowAsync<BadRequestException>();
    }


    /* ══════════════ AVTOMATIK ODENIS (Hangfire) ══════════════ */

    [Fact]
    public async Task ProcessDuePaymentsAsync_VaxtiCatmisOdenis_HesabdanTutulur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var loan = Loan(acc.Id, term: 12);
        loan.Status = LoanStatus.Active;
        loan.RemainingBalance = 1344;
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        db.LoanPayments.Add(new LoanPayment
        {
            LoanId = loan.Id,
            PaymentNumber = 1,
            DueDate = DateTime.UtcNow.AddDays(-1),   // vaxti kecib
            Amount = 112,
            IsPaid = false
        });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        acc.Balance.Should().Be(388);            // 500 - 112
        loan.RemainingBalance.Should().Be(1232); // 1344 - 112
        db.LoanPayments.Single().IsPaid.Should().BeTrue();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_VaxtiCatmayibsa_ToxunulmurTutulmur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var loan = Loan(acc.Id);
        loan.Status = LoanStatus.Active;
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        db.LoanPayments.Add(new LoanPayment
        {
            LoanId = loan.Id,
            PaymentNumber = 1,
            DueDate = DateTime.UtcNow.AddDays(10),   // gelecekde
            Amount = 112,
            IsPaid = false
        });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        acc.Balance.Should().Be(500);
        db.LoanPayments.Single().IsPaid.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_BalansKifayetEtmirse_Atlanir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 50);   // 112 lazimdir
        var loan = Loan(acc.Id);
        loan.Status = LoanStatus.Active;
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        db.LoanPayments.Add(new LoanPayment
        {
            LoanId = loan.Id,
            PaymentNumber = 1,
            DueDate = DateTime.UtcNow.AddDays(-1),
            Amount = 112,
            IsPaid = false
        });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        // balans MENFIYE dusmemelidir — odenis novbeti dovre qalir
        acc.Balance.Should().Be(50);
        db.LoanPayments.Single().IsPaid.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_SonOdenisden_KreditBaglanir()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var loan = Loan(acc.Id, term: 1);
        loan.Status = LoanStatus.Active;
        loan.RemainingBalance = 112;   // yalniz bir odenis qalib
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        db.LoanPayments.Add(new LoanPayment
        {
            LoanId = loan.Id,
            PaymentNumber = 1,
            DueDate = DateTime.UtcNow.AddDays(-1),
            Amount = 112,
            IsPaid = false
        });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        loan.RemainingBalance.Should().Be(0);
        loan.Status.Should().Be(LoanStatus.Completed);   // borc bitdi
    }

    [Fact]
    public async Task ProcessDuePaymentsAsync_AktivOlmayanKredit_Toxunulmur()
    {
        using var db = TestDb.Create();
        var acc = Account(balance: 500);
        var loan = Loan(acc.Id, status: LoanStatus.Pending);   // hele tesdiqlenmeyib
        db.Accounts.Add(acc);
        db.Loans.Add(loan);
        db.LoanPayments.Add(new LoanPayment
        {
            LoanId = loan.Id,
            PaymentNumber = 1,
            DueDate = DateTime.UtcNow.AddDays(-1),
            Amount = 112,
            IsPaid = false
        });
        await db.SaveChangesAsync();
        var sut = CreateSut(db);

        await sut.ProcessDuePaymentsAsync();

        // tesdiqlenmemis kredite gore pul tutulmamalidir
        acc.Balance.Should().Be(500);
        db.LoanPayments.Single().IsPaid.Should().BeFalse();
    }
}