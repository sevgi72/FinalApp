using BankApiApp.Data;
using BankApiApp.DTOs.Utility;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BankApiApp.Services;

public class PaymentTemplateService : IPaymentTemplateService
{
    private readonly AppDbContext _context;
    private readonly ILocalizationService _localizer;

    public PaymentTemplateService(AppDbContext context, ILocalizationService localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    public async Task<PaymentTemplateDto> CreateAsync(string userId, CreatePaymentTemplateDto dto)
    {
        var provider = await _context.UtilityProviders
            .FirstOrDefaultAsync(p => p.Id == dto.ProviderId && p.IsActive)
            ?? throw new NotFoundException(_localizer.Get("ProviderNotFound"));

        var template = new PaymentTemplate
        {
            UserId = userId,
            Name = dto.Name,
            ProviderId = dto.ProviderId,
            SubscriberCode = dto.SubscriberCode
        };

        _context.PaymentTemplates.Add(template);
        await _context.SaveChangesAsync();

        return new PaymentTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            ProviderId = provider.Id,
            ProviderName = provider.Name,
            Category = provider.Category.ToString(),
            SubscriberCode = template.SubscriberCode
        };
    }

    public async Task<List<PaymentTemplateDto>> GetMyAsync(string userId)
    {
        return await _context.PaymentTemplates
            .Include(t => t.Provider)
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new PaymentTemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                ProviderId = t.ProviderId,
                ProviderName = t.Provider.Name,
                Category = t.Provider.Category.ToString(),
                SubscriberCode = t.SubscriberCode
            })
            .ToListAsync();
    }

    public async Task<string> DeleteAsync(string userId, Guid id)
    {
        var template = await _context.PaymentTemplates
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId)
            ?? throw new NotFoundException(_localizer.Get("PaymentNotFound"));

        _context.PaymentTemplates.Remove(template);
        await _context.SaveChangesAsync();

        return "Sablon silindi.";
    }
}