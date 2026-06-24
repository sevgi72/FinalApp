using AutoMapper;
using BankApiApp.Data;
using BankApiApp.DTOs.Account;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class AccountService : IAccountService
{
    private readonly AppDbContext _context;
    private readonly IMapper _mapper;

    public AccountService(AppDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<AccountDto> CreateAccountAsync(string userId, CreateAccountDto dto)
    {
        var account = new Account
        {
            AccountNumber = GenerateAccountNumber(),
            Balance = 0,
            Currency = dto.Currency,
            Type = dto.Type,
            Status = AccountStatus.Active,
            OwnerId = userId
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return _mapper.Map<AccountDto>(account);
    }

    public async Task<List<AccountDto>> GetMyAccountsAsync(string userId)
    {
        var accounts = await _context.Accounts
            .Where(a => a.OwnerId == userId)
            .ToListAsync();

        return _mapper.Map<List<AccountDto>>(accounts);
    }

    public async Task<AccountDto> GetByIdAsync(string userId, Guid accountId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.OwnerId == userId);

        if (account == null)
            throw new NotFoundException("Hesab tapılmadı.");

        return _mapper.Map<AccountDto>(account);
    }

    private string GenerateAccountNumber()
    {
        var digits = string.Concat(Enumerable.Range(0, 16).Select(_ => Random.Shared.Next(0, 10)));
        return "AZ" + digits;
    }
}