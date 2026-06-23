using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Transaction;
using BankApiApp.DTOs.Transactions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public TransactionService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<TransactionDto> DepositAsync(string userId, DepositDto dto)
    {
        if (dto.Amount <= 0)
            throw new Exception("Məbləğ 0-dan böyük olmalıdır.");

        var account = await GetOwnedAccountAsync(userId, dto.AccountId);
        account.Balance += dto.Amount;

        var transaction = new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            ToAccountId = account.Id,
            Description = "Deposit"
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return _mapper.Map<TransactionDto>(transaction);
    }

    public async Task<TransactionDto> WithdrawAsync(string userId, WithdrawDto dto)
    {
        if (dto.Amount <= 0)
            throw new Exception("Məbləğ 0-dan böyük olmalıdır.");

        var account = await GetOwnedAccountAsync(userId, dto.AccountId);

        if (account.Balance < dto.Amount)
            throw new Exception("Balans kifayət etmir.");

        account.Balance -= dto.Amount;

        var transaction = new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Withdraw,
            Status = TransactionStatus.Completed,
            FromAccountId = account.Id,
            Description = "Withdraw"
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return _mapper.Map<TransactionDto>(transaction);
    }

    public async Task<TransactionDto> TransferAsync(string userId, TransferDto dto)
    {
        if (dto.Amount <= 0)
            throw new Exception("Məbləğ 0-dan böyük olmalıdır.");

        var fromAccount = await GetOwnedAccountAsync(userId, dto.FromAccountId);

        var toAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountNumber == dto.ToAccountNumber);

        if (toAccount == null)
            throw new Exception("Alan hesab tapılmadı.");
        if (fromAccount.Id == toAccount.Id)
            throw new Exception("Eyni hesaba köçürmə olmaz.");
        if (fromAccount.Balance < dto.Amount)
            throw new Exception("Balans kifayət etmir.");

        // ATOMIC: ya hamısı baş verir, ya heç biri
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            fromAccount.Balance -= dto.Amount;
            toAccount.Balance += dto.Amount;

            var transaction = new Transaction
            {
                Amount = dto.Amount,
                Type = TransactionType.Transfer,
                Status = TransactionStatus.Completed,
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                Description = dto.Description
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            await dbTransaction.CommitAsync();
            return _mapper.Map<TransactionDto>(transaction);
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    // ortaq köməkçi: hesab istifadəçiyə aiddir və aktivdir?
    private async Task<Account> GetOwnedAccountAsync(string userId, Guid accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId);

        if (account == null)
            throw new Exception("Hesab tapılmadı.");
        if (account.Status != AccountStatus.Active)
            throw new Exception("Hesab aktiv deyil.");

        return account;
    }
    public async Task<List<TransactionDto>> GetAccountTransactionsAsync(string userId, Guid accountId)
    {
        // hesab istifadəçiyə aiddir? (təhlükəsizlik)
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId);

        if (account == null)
            throw new Exception("Hesab tapılmadı.");

        // bu hesabın həm göndərdiyi, həm aldığı əməliyyatlar
        var transactions = await _context.Transactions
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)   // ən yeni yuxarıda
            .ToListAsync();

        return _mapper.Map<List<TransactionDto>>(transactions);
    }

    public async Task TransferByCardAsync(string userId, TransferByCardDto dto)
    {
        if (dto.Amount <= 0)
            throw new Exception("Məbləğ sıfırdan böyük olmalıdır.");

        // 1. Göndərənin hesabı + sahiblik
        var fromAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.FromAccountId)
            ?? throw new Exception("Göndərən hesab tapılmadı.");

        if (fromAccount.OwnerId != userId)
            throw new Exception("Bu hesab sizə aid deyil.");

        // 2. Kart nömrəsindən alıcının kartını + bağlı hesabını tap
        var cardNumber = (dto.ToCardNumber ?? string.Empty).Replace(" ", "");

        var toCard = await _context.Cards
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.CardNumber == cardNumber)
            ?? throw new Exception("Bu nömrə ilə kart tapılmadı.");

        if (toCard.Status == CardStatus.Blocked)
            throw new Exception("Alıcının kartı bloklanıb.");

        var toAccount = toCard.Account;

        // 3. Özünə köçürmə qadağası
        if (toAccount.Id == fromAccount.Id)
            throw new Exception("Eyni hesaba köçürmə edilə bilməz.");

        // 4. Balans yoxlaması
        if (fromAccount.Balance < dto.Amount)
            throw new Exception("Balans kifayət deyil.");

        // 5. Atomik köçürmə
        await using var trx = await _context.Database.BeginTransactionAsync();

        fromAccount.Balance -= dto.Amount;
        toAccount.Balance += dto.Amount;

        _context.Transactions.Add(new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            Description = dto.Description ?? $"Karta köçürmə (**** {cardNumber[^4..]})",
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();
    }
    public async Task<ReceiptDto> GetReceiptAsync(string userId, Guid transactionId)
    {
        var trx = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId)
            ?? throw new Exception("Əməliyyat tapılmadı.");

        // sahiblik: istifadəçi from və ya to hesabın sahibi olmalıdır
        var myAccountIds = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        bool ownsFrom = trx.FromAccountId.HasValue && myAccountIds.Contains(trx.FromAccountId.Value);
        bool ownsTo = trx.ToAccountId.HasValue && myAccountIds.Contains(trx.ToAccountId.Value);
        if (!ownsFrom && !ownsTo)
            throw new Exception("Bu əməliyyat sizə aid deyil.");

        // göstərmək üçün hesab nömrələrini tap
        string? fromNumber = null, toNumber = null;
        if (trx.FromAccountId.HasValue)
            fromNumber = await _context.Accounts
                .Where(a => a.Id == trx.FromAccountId.Value)
                .Select(a => a.AccountNumber).FirstOrDefaultAsync();
        if (trx.ToAccountId.HasValue)
            toNumber = await _context.Accounts
                .Where(a => a.Id == trx.ToAccountId.Value)
                .Select(a => a.AccountNumber).FirstOrDefaultAsync();

        return new ReceiptDto
        {
            Reference = "TRX-" + trx.Id.ToString("N")[..12].ToUpper(),
            Date = trx.CreatedAt,
            Type = trx.Type.ToString(),
            Status = trx.Status.ToString(),
            Amount = trx.Amount,
            FromAccount = MaskAccount(fromNumber),
            ToAccount = MaskAccount(toNumber),
            Description = trx.Description
        };
    }

    public async Task<(byte[] Pdf, string FileName)> GetReceiptPdfAsync(string userId, Guid transactionId)
    {
        var receipt = await GetReceiptAsync(userId, transactionId);
        var pdf = ReceiptPdfGenerator.Generate(receipt);
        var fileName = $"qebz-{receipt.Reference}.pdf";
        return (pdf, fileName);
    }

    // hesab nömrəsini maskala (son 4 rəqəm)
    private static string? MaskAccount(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber)) return null;
        return accountNumber.Length <= 4 ? accountNumber : "**** " + accountNumber[^4..];
    }
}