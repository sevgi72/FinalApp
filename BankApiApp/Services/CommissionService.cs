using BankApiApp.Data;
using BankApiApp.DTOs.Commission;
using BankApiApp.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class CommissionService : ICommissionService
{
    private readonly AppDbContext _context;

    public CommissionService(AppDbContext context)
    {
        _context = context;
    }

    // emeliyyat ucun komissiya hesabla
    public async Task<decimal> CalculateAsync(string transactionType, decimal amount)
    {
        var rule = await _context.CommissionRules
            .FirstOrDefaultAsync(r => r.TransactionType == transactionType && r.IsActive);

        // qayda yoxdursa ve ya mebleg hedden asagidirsa - komissiya yoxdur
        if (rule == null || amount < rule.MinAmount)
            return 0;

        var commission = Math.Round(amount * rule.Percentage, 2);

        // tavani asmasin
        if (rule.MaxCommission > 0 && commission > rule.MaxCommission)
            commission = rule.MaxCommission;

        return commission;
    }

    public async Task<List<CommissionRuleDto>> GetRulesAsync()
    {
        return await _context.CommissionRules
            .Select(r => new CommissionRuleDto
            {
                Id = r.Id,
                TransactionType = r.TransactionType,
                MinAmount = r.MinAmount,
                Percentage = r.Percentage,
                MaxCommission = r.MaxCommission,
                IsActive = r.IsActive
            })
            .ToListAsync();
    }

    public async Task<string> UpdateRuleAsync(Guid id, UpdateCommissionRuleDto dto)
    {
        var rule = await _context.CommissionRules
            .FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new NotFoundException("Komissiya qaydasi tapilmadi.");

        rule.MinAmount = dto.MinAmount;
        rule.Percentage = dto.Percentage;
        rule.MaxCommission = dto.MaxCommission;
        rule.IsActive = dto.IsActive;
        await _context.SaveChangesAsync();

        return "Komissiya qaydasi yenilendi.";
    }
}