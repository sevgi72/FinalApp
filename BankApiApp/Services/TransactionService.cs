using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Common;
using BankApiApp.DTOs.Transaction;
using BankApiApp.DTOs.Transactions;
using BankApiApp.Exceptions;
using BankApiApp.Extensions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;
    private readonly ILimitService _limitService;
    private readonly IRoundUpService _roundUpService;
    private readonly ILocalizationService _localizer;
    private readonly INotificationService _notificationService;
    public TransactionService(AppDbContext context, IMapper mapper, ILimitService limitService, IRoundUpService roundUpService,
        ILocalizationService localizer, INotificationService notificationService)
    {
        _context = context;
        _mapper = mapper;
        _limitService = limitService;
        _roundUpService = roundUpService;
        _localizer = localizer;
        _notificationService = notificationService;
    }
    public async Task<PagedResult<TransactionDto>> GetAccountTransactionsAsync(
    string userId, Guid accountId, PaginationParams pagination)
    {
        // hesab istifadeciye aiddir? (tehlukesizlik)
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId);

        if (account == null)
            throw new NotFoundException(_localizer.Get("AccountNotFound"));

        // sorgunu qur (hele icra olunmur)
        var query = _context.Transactions
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt);   // en yeni yuxarida

        // entity seviyyesinde sehifele (Skip/Take bazada isleyir)
        var paged = await query.ToPagedResultAsync(pagination);

        // bu sehifenin element-lerini DTO-ya map et
        return new PagedResult<TransactionDto>
        {
            Items = _mapper.Map<List<TransactionDto>>(paged.Items),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
    }
    public async Task<TransactionDto> DepositAsync(string userId, DepositDto dto)
    {
        if (dto.Amount <= 0)
            throw new BadRequestException(_localizer.Get("AmountMustBePositive"));

        var account = await GetOwnedAccountAsync(userId, dto.AccountId);

        // Balansı artırırıq
        account.Balance += dto.Amount;

        var transaction = new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            ToAccountId = account.Id,
            ToBalanceSnapshot = account.Balance,   // yatirimdan sonra alan hesabin balansi
            Description = "Deposit"
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // notifikasiya gonderirik (istifadeciye)
        await _notificationService.SendToUserAsync(
            userId,
            "Balans artimi",
            $"Hesabiniza {dto.Amount:0.00} AZN daxil oldu.",
            "Transaction");

        return _mapper.Map<TransactionDto>(transaction);
    }

    public async Task<TransactionDto> WithdrawAsync(string userId, WithdrawDto dto)
    {
        if (dto.Amount <= 0)
            throw new BadRequestException(_localizer.Get("AmountMustBePositive"));

        var account = await GetOwnedAccountAsync(userId, dto.AccountId);

        if (account.Balance < dto.Amount)
            throw new BadRequestException(_localizer.Get("InsufficientBalance"));

        // limit yoxlamasi (gunluk/ayliq/illik)
        await _limitService.EnsureWithinLimitsAsync(account, dto.Amount);

        // Balansı azaldırıq
        account.Balance -= dto.Amount;

        var transaction = new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Withdraw,
            Status = TransactionStatus.Completed,
            FromAccountId = account.Id,
            FromBalanceSnapshot = account.Balance, // gonderen hesabin anlıq balansi
            Description = "Withdraw"
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // esas emeliyatdan sonra yigim cehdini edirik (round-up)
        await _roundUpService.TryProcessAsync(account, dto.Amount);

        return _mapper.Map<TransactionDto>(transaction);
    }

    public async Task<TransactionDto> TransferAsync(string userId, TransferDto dto)
    {
        if (dto.Amount <= 0)
            throw new BadRequestException(_localizer.Get("AmountMustBePositive"));

        var fromAccount = await GetOwnedAccountAsync(userId, dto.FromAccountId);

        var toAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountNumber == dto.ToAccountNumber);

        if (toAccount == null)
            throw new NotFoundException(_localizer.Get("ReceiverAccountNotFound"));
        if (fromAccount.Id == toAccount.Id)
            throw new BadRequestException(_localizer.Get("SameAccountTransfer"));
        if (fromAccount.Balance < dto.Amount)
            throw new BadRequestException(_localizer.Get("InsufficientBalance"));

        //limit yoxlamasi (gunluk/ayliq/illik)
        await _limitService.EnsureWithinLimitsAsync(fromAccount, dto.Amount);

        //ya hamisi bas verir, ya hec biri
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Balansları dəyişirik
            fromAccount.Balance -= dto.Amount;
            toAccount.Balance += dto.Amount;

            var transaction = new Transaction
            {
                Amount = dto.Amount,
                Type = TransactionType.Transfer,
                Status = TransactionStatus.Completed,
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id,
                FromBalanceSnapshot = fromAccount.Balance, 
                ToBalanceSnapshot = toAccount.Balance,     
                Description = dto.Description
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            await dbTransaction.CommitAsync();

            await _roundUpService.TryProcessAsync(fromAccount, dto.Amount);

            return _mapper.Map<TransactionDto>(transaction);
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    public async Task TransferByCardAsync(string userId, TransferByCardDto dto)
    {
        if (dto.Amount <= 0)
            throw new BadRequestException(_localizer.Get("AmountMustBePositive"));

        var fromAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.FromAccountId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (fromAccount.OwnerId != userId)
            throw new BadRequestException(_localizer.Get("AccountNotYours"));

        var cardNumber = (dto.ToCardNumber ?? string.Empty).Replace(" ", "");

        var toCard = await _context.Cards
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.CardNumber == cardNumber)
            ?? throw new NotFoundException(_localizer.Get("CardNotFound"));

        if (toCard.Status == CardStatus.Blocked)
            throw new BadRequestException(_localizer.Get("ReceiverCardBlocked"));

        var toAccount = toCard.Account;

        if (toAccount.Id == fromAccount.Id)
            throw new BadRequestException(_localizer.Get("SameAccountTransfer"));

        if (fromAccount.Balance < dto.Amount)
            throw new BadRequestException(_localizer.Get("InsufficientBalance"));

        await _limitService.EnsureWithinLimitsAsync(fromAccount, dto.Amount);

        await using var trx = await _context.Database.BeginTransactionAsync();
        try
        {
            fromAccount.Balance -= dto.Amount;
            toAccount.Balance += dto.Amount;

            _context.Transactions.Add(new Transaction
            {
                Amount = dto.Amount,
                Type = TransactionType.Transfer,
                Status = TransactionStatus.Completed,
                Description = dto.Description ?? _localizer.Get("DefaultTransferDescription").Replace("{cardNumber}", $"**** {cardNumber[^4..]}"),
                FromAccountId = fromAccount.Id,
                ToAccountId = toAccount.Id
            });

            await _context.SaveChangesAsync();
            await trx.CommitAsync();

            await _roundUpService.TryProcessAsync(fromAccount, dto.Amount);
        }
        catch
        {
            await trx.RollbackAsync();
            throw;
        }
    }

    public async Task<ReceiptDto> GetReceiptAsync(string userId, Guid transactionId)
    {
        var trx = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId)
            ?? throw new NotFoundException(_localizer.Get("TransactionNotFound"));

        var myAccountIds = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        bool ownsFrom = trx.FromAccountId.HasValue && myAccountIds.Contains(trx.FromAccountId.Value);
        bool ownsTo = trx.ToAccountId.HasValue && myAccountIds.Contains(trx.ToAccountId.Value);
        if (!ownsFrom && !ownsTo)
            throw new BadRequestException(_localizer.Get("TransactionNotYours"));

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

    private static string? MaskAccount(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber)) return null;
        return accountNumber.Length <= 4 ? accountNumber : "**** " + accountNumber[^4..];
    }

    public async Task<(byte[] Content, string FileName)> ExportTransactionsCsvAsync(string userId, Guid accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        var transactions = await _context.Transactions
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Tarix,Nov,Mebleg,Status,Tesvir");

        foreach (var t in transactions)
        {
            var date = t.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            var description = (t.Description ?? "").Replace("\"", "\"\"");
            sb.AppendLine($"{date},{t.Type},{t.Amount},{t.Status},\"{description}\"");
        }

        var preamble = System.Text.Encoding.UTF8.GetPreamble();
        var body = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var content = preamble.Concat(body).ToArray();

        var fileName = $"emeliyyatlar-{account.AccountNumber}.csv";
        return (content, fileName);
    }

    private async Task<Account> GetOwnedAccountAsync(string userId, Guid accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId);

        if (account == null)
            throw new NotFoundException(_localizer.Get("AccountNotFound"));
        if (account.Status != AccountStatus.Active)
            throw new BadRequestException(_localizer.Get("AccountNotActive"));

        return account;
    }
    // oz hesablari arasi kocurme (limitsiz - oz pulu)
    public async Task<TransactionDto> SelfTransferAsync(string userId, SelfTransferDto dto)
    {
        if (dto.Amount <= 0)
            throw new BadRequestException(_localizer.Get("AmountMustBePositive"));

        if (dto.FromAccountId == dto.ToAccountId)
            throw new BadRequestException(_localizer.Get("SameAccountTransfer"));

        // her iki hesab da bu istifadeciye aid olmalidir
        var fromAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.FromAccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        var toAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.ToAccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (fromAccount.Status != AccountStatus.Active || toAccount.Status != AccountStatus.Active)
            throw new BadRequestException(_localizer.Get("AccountNotActive"));

        if (fromAccount.Balance < dto.Amount)
            throw new BadRequestException(_localizer.Get("InsufficientBalance"));

        // atomik kocurme (limit yoxlamasi yoxdur - oz pulu)
        await using var trx = await _context.Database.BeginTransactionAsync();

        fromAccount.Balance -= dto.Amount;
        toAccount.Balance += dto.Amount;

        var transaction = new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            FromAccountId = fromAccount.Id,
            ToAccountId = toAccount.Id,
            Description = "Hesablar arasi kocurme"
        };
        _context.Transactions.Add(transaction);

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return _mapper.Map<TransactionDto>(transaction);
    }
}