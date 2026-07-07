using BankApiApp.Data;
using BankApiApp.DTOs.SavingsGoal;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class SavingsGoalService : ISavingsGoalService
{
    private readonly AppDbContext _context;
    private readonly ILocalizationService _localizer;
    private readonly INotificationService _notificationService;

    public SavingsGoalService(AppDbContext context,
        ILocalizationService localizer,
        INotificationService notificationService)
    {
        _context = context;
        _localizer = localizer;
        _notificationService = notificationService;
    }

    // yeni meqsed yarat
    public async Task<SavingsGoalDto> CreateAsync(string userId, CreateSavingsGoalDto dto)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.SourceAccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        var goal = new SavingsGoal
        {
            UserId = userId,
            Name = dto.Name,
            TargetAmount = dto.TargetAmount,
            CurrentAmount = 0,
            SourceAccountId = dto.SourceAccountId,
            Deadline = dto.Deadline.HasValue
                ? DateTime.SpecifyKind(dto.Deadline.Value, DateTimeKind.Utc)
                : null,
            IsCompleted = false
        };

        _context.SavingsGoals.Add(goal);
        await _context.SaveChangesAsync();

        return MapToDto(goal);
    }

    public async Task<List<SavingsGoalDto>> GetMyAsync(string userId)
    {
        var goals = await _context.SavingsGoals
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();

        return goals.Select(MapToDto).ToList();
    }

    // meqsede pul elave et (esas hesabdan cixir)
    public async Task<SavingsGoalDto> ContributeAsync(string userId, Guid goalId, ContributeGoalDto dto)
    {
        var goal = await _context.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId)
            ?? throw new NotFoundException("Meqsed tapilmadi.");

        if (goal.IsCompleted)
            throw new BadRequestException("Bu meqsed artiq tamamlanib.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == goal.SourceAccountId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        if (account.Balance < dto.Amount)
            throw new BadRequestException(_localizer.Get("InsufficientBalance"));

        await using var trx = await _context.Database.BeginTransactionAsync();

        account.Balance -= dto.Amount;
        goal.CurrentAmount += dto.Amount;

        // emeliyyat tarixcesi
        _context.Transactions.Add(new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Withdraw,
            Status = TransactionStatus.Completed,
            FromAccountId = account.Id,
            Description = $"Meqsede yigim: {goal.Name}"
        });

        // hedefe catdi?
        bool justCompleted = false;
        if (goal.CurrentAmount >= goal.TargetAmount && !goal.IsCompleted)
        {
            goal.IsCompleted = true;
            justCompleted = true;
        }

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        // hedefe catanda bildiris gonder
        if (justCompleted)
        {
            await _notificationService.SendToUserAsync(userId,
                "Meqsed tamamlandi!",
                $"'{goal.Name}' meqsedine catdiniz ({goal.TargetAmount:0.00} AZN). Tebrikler!",
                "System");
        }

        return MapToDto(goal);
    }

    // meqseddan pul cixar (esas hesaba qayidir)
    public async Task<string> WithdrawAsync(string userId, Guid goalId, ContributeGoalDto dto)
    {
        var goal = await _context.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId)
            ?? throw new NotFoundException("Meqsed tapilmadi.");

        if (goal.CurrentAmount < dto.Amount)
            throw new BadRequestException("Meqseddeki mebleg kifayet etmir.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == goal.SourceAccountId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        await using var trx = await _context.Database.BeginTransactionAsync();

        goal.CurrentAmount -= dto.Amount;
        account.Balance += dto.Amount;

        // hedefden asagi dususe, "tamamlanmis" statusunu geri al
        if (goal.CurrentAmount < goal.TargetAmount)
            goal.IsCompleted = false;

        _context.Transactions.Add(new Transaction
        {
            Amount = dto.Amount,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            ToAccountId = account.Id,
            Description = $"Meqseddan cixaris: {goal.Name}"
        });

        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return $"{dto.Amount:0.00} AZN hesabiniza qaytarildi.";
    }

    // meqsedi sil (icindeki pul esas hesaba qayidir)
    public async Task<string> DeleteAsync(string userId, Guid goalId)
    {
        var goal = await _context.SavingsGoals
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId)
            ?? throw new NotFoundException("Meqsed tapilmadi.");

        await using var trx = await _context.Database.BeginTransactionAsync();

        // icinde pul varsa esas hesaba qaytar
        if (goal.CurrentAmount > 0)
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == goal.SourceAccountId);
            if (account != null)
            {
                account.Balance += goal.CurrentAmount;
                _context.Transactions.Add(new Transaction
                {
                    Amount = goal.CurrentAmount,
                    Type = TransactionType.Deposit,
                    Status = TransactionStatus.Completed,
                    ToAccountId = account.Id,
                    Description = $"Meqsed silindi, mebleg qaytarildi: {goal.Name}"
                });
            }
        }

        _context.SavingsGoals.Remove(goal);
        await _context.SaveChangesAsync();
        await trx.CommitAsync();

        return "Meqsed silindi, mebleg hesabiniza qaytarildi.";
    }

    private static SavingsGoalDto MapToDto(SavingsGoal g) => new()
    {
        Id = g.Id,
        Name = g.Name,
        TargetAmount = g.TargetAmount,
        CurrentAmount = g.CurrentAmount,
        Percentage = g.TargetAmount > 0
            ? Math.Round(g.CurrentAmount / g.TargetAmount * 100, 1)
            : 0,
        IsCompleted = g.IsCompleted,
        Deadline = g.Deadline,
        CreatedAt = g.CreatedAt
    };
}