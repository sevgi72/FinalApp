using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Common;
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
    private readonly ILimitService _limitService;   // ← yeni

    public TransactionService(AppDbContext context, IMapper mapper, ILimitService limitService)
    {
        _context = context;
        _mapper = mapper;
        _limitService = limitService;   // ← yeni
    }

    public async Task<TransactionDto> DepositAsync(string userId, DepositDto dto)
    {
        if (dto.Amount <= 0)
            throw new BadRequestException("Məbləğ 0-dan böyük olmalıdır.");

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
            throw new BadRequestException("Məbləğ 0-dan böyük olmalıdır.");

        var account = await GetOwnedAccountAsync(userId, dto.AccountId);

        if (account.Balance < dto.Amount)
            throw new BadRequestException("Balans kifayət etmir.");

        // limit yoxlaması (günlük/aylıq/illik)
        await _limitService.EnsureWithinLimitsAsync(account, dto.Amount);

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
            throw new BadRequestException("Məbləğ 0-dan böyük olmalıdır.");

        var fromAccount = await GetOwnedAccountAsync(userId, dto.FromAccountId);

        var toAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.AccountNumber == dto.ToAccountNumber);

        if (toAccount == null)
            throw new NotFoundException("Alan hesab tapılmadı.");
        if (fromAccount.Id == toAccount.Id)
            throw new BadRequestException("Eyni hesaba köçürmə olmaz.");
        if (fromAccount.Balance < dto.Amount)
            throw new BadRequestException("Balans kifayət etmir.");

        // limit yoxlaması (günlük/aylıq/illik)
        await _limitService.EnsureWithinLimitsAsync(fromAccount, dto.Amount);

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
            throw new NotFoundException("Hesab tapılmadı.");
        if (account.Status != AccountStatus.Active)
            throw new BadRequestException("Hesab aktiv deyil.");

        return account;
    }

    public async Task<PagedResult<TransactionDto>> GetAccountTransactionsAsync(
        string userId, Guid accountId, PaginationParams pagination)
    {
        // hesab istifadəçiyə aiddir? (təhlükəsizlik)
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId);

        if (account == null)
            throw new NotFoundException("Hesab tapılmadı.");

        // sorğunu qur (hələ icra olunmur)
        var query = _context.Transactions
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt);   // ən yeni yuxarıda

        // entity səviyyəsində səhifələ (Skip/Take bazada işləyir)
        var paged = await query.ToPagedResultAsync(pagination);

        // bu səhifənin element-lərini DTO-ya map et və yeni nəticə qur
        return new PagedResult<TransactionDto>
        {
            Items = _mapper.Map<List<TransactionDto>>(paged.Items),
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            TotalPages = paged.TotalPages
        };
    }

    public async Task TransferByCardAsync(string userId, TransferByCardDto dto)
    {
        if (dto.Amount <= 0)
            throw new BadRequestException("Məbləğ sıfırdan böyük olmalıdır.");

        // 1. Göndərənin hesabı + sahiblik
        var fromAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.FromAccountId)
            ?? throw new NotFoundException("Göndərən hesab tapılmadı.");

        if (fromAccount.OwnerId != userId)
            throw new BadRequestException("Bu hesab sizə aid deyil.");

        // 2. Kart nömrəsindən alıcının kartını + bağlı hesabını tap
        var cardNumber = (dto.ToCardNumber ?? string.Empty).Replace(" ", "");

        var toCard = await _context.Cards
            .Include(c => c.Account)
            .FirstOrDefaultAsync(c => c.CardNumber == cardNumber)
            ?? throw new NotFoundException("Bu nömrə ilə kart tapılmadı.");

        if (toCard.Status == CardStatus.Blocked)
            throw new BadRequestException("Alıcının kartı bloklanıb.");

        var toAccount = toCard.Account;

        // 3. Özünə köçürmə qadağası
        if (toAccount.Id == fromAccount.Id)
            throw new BadRequestException("Eyni hesaba köçürmə edilə bilməz.");

        // 4. Balans yoxlaması
        if (fromAccount.Balance < dto.Amount)
            throw new BadRequestException("Balans kifayət deyil.");

        // 5. limit yoxlaması (günlük/aylıq/illik)
        await _limitService.EnsureWithinLimitsAsync(fromAccount, dto.Amount);

        // 6. Atomik köçürmə
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
            ?? throw new NotFoundException("Əməliyyat tapılmadı.");

        // sahiblik: istifadəçi from və ya to hesabın sahibi olmalıdır
        var myAccountIds = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .Select(a => a.Id)
            .ToListAsync();

        bool ownsFrom = trx.FromAccountId.HasValue && myAccountIds.Contains(trx.FromAccountId.Value);
        bool ownsTo = trx.ToAccountId.HasValue && myAccountIds.Contains(trx.ToAccountId.Value);
        if (!ownsFrom && !ownsTo)
            throw new BadRequestException("Bu əməliyyat sizə aid deyil.");

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

    public async Task<(byte[] Content, string FileName)> ExportTransactionsCsvAsync(
        string userId, Guid accountId)
    {
        // hesab istifadəçiyə aiddir? (təhlükəsizlik)
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId)
            ?? throw new NotFoundException("Hesab tapılmadı.");

        var transactions = await _context.Transactions
            .Where(t => t.FromAccountId == accountId || t.ToAccountId == accountId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Tarix,Növ,Məbləğ,Status,Təsvir");   // başlıq sətri

        foreach (var t in transactions)
        {
            var date = t.CreatedAt.ToString("dd.MM.yyyy HH:mm");
            var description = (t.Description ?? "").Replace("\"", "\"\"");   // dırnaqları qoru
            sb.AppendLine($"{date},{t.Type},{t.Amount},{t.Status},\"{description}\"");
        }

        // UTF-8 BOM əlavə et ki, Excel Azərbaycan hərflərini düzgün göstərsin
        var preamble = System.Text.Encoding.UTF8.GetPreamble();
        var body = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        var content = preamble.Concat(body).ToArray();

        var fileName = $"emeliyyatlar-{account.AccountNumber}.csv";
        return (content, fileName);
    }
}