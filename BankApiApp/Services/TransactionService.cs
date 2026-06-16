using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Transaction;
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
}