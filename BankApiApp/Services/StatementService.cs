using BankApiApp.Data;
using BankApiApp.DTOs.Statement;
using BankApiApp.Exceptions;
using BankApiApp.Models;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BankApiApp.Services;

public class StatementService : IStatementService
{
    private readonly AppDbContext _context;
    private readonly ILocalizationService _localizer;

    public StatementService(AppDbContext context, ILocalizationService localizer)
    {
        _context = context;
        _localizer = localizer;
    }

    // hesabat yarat (secilen tarix araligi)
    public async Task<StatementDto> GenerateAsync(string userId, StatementRequestDto dto)
    {
        // hesab istifadeciye aiddir?
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.OwnerId == userId)
            ?? throw new NotFoundException(_localizer.Get("AccountNotFound"));

        // tarixleri UTC-ye getir (PostgreSQL ucun)
        var from = DateTime.SpecifyKind(dto.From.Date, DateTimeKind.Utc);
        var to = DateTime.SpecifyKind(dto.To.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc); // gunun sonu

        if (from > to)
            throw new BadRequestException("Baslangic tarixi son tarixden boyuk ola bilmez.");

        // bu hesaba aid butun emeliyyatlar (tarixe gore)
        var allTx = await _context.Transactions
            .Where(t => (t.FromAccountId == account.Id || t.ToAccountId == account.Id)
                     && t.Status == TransactionStatus.Completed)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync();

        // acilis balansi = cari balans - dovrden sonraki emeliyyatlarin tesiri
        // sade usul: dovrden EVVELki emeliyyatlarin cemini hesabla
        decimal openingBalance = 0;
        foreach (var t in allTx.Where(t => t.CreatedAt < from))
        {
            if (t.ToAccountId == account.Id) openingBalance += t.Amount;    // daxil olan
            if (t.FromAccountId == account.Id) openingBalance -= t.Amount;  // cixan
        }

        // dovr icindeki emeliyyatlar
        var periodTx = allTx.Where(t => t.CreatedAt >= from && t.CreatedAt <= to).ToList();

        var lines = new List<StatementLineDto>();
        decimal totalIn = 0, totalOut = 0;
        decimal running = openingBalance;

        foreach (var t in periodTx)
        {
            bool isIn = t.ToAccountId == account.Id;
            running += isIn ? t.Amount : -t.Amount;

            if (isIn) totalIn += t.Amount;
            else totalOut += t.Amount;

            lines.Add(new StatementLineDto
            {
                Date = t.CreatedAt,
                Type = t.Type.ToString(),
                Description = t.Description,
                Amount = t.Amount,
                Direction = isIn ? "In" : "Out",
                BalanceAfter = running
            });
        }

        return new StatementDto
        {
            AccountNumber = account.AccountNumber,
            Currency = account.Currency,
            From = from,
            To = to,
            OpeningBalance = openingBalance,
            ClosingBalance = running,
            TotalIn = totalIn,
            TotalOut = totalOut,
            TransactionCount = periodTx.Count,
            Lines = lines
        };
    }

    // hesabati PDF kimi yarat
    public async Task<byte[]> GeneratePdfAsync(string userId, StatementRequestDto dto)
    {
        var s = await GenerateAsync(userId, dto);
        QuestPDF.Settings.License = LicenseType.Community;

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                // basliq
                page.Header().Column(col =>
                {
                    col.Item().Text("BankApiApp - Hesab Cixarisi").FontSize(18).Bold();
                    col.Item().Text($"Hesab: {s.AccountNumber} ({s.Currency})");
                    col.Item().Text($"Dovr: {s.From:dd.MM.yyyy} - {s.To:dd.MM.yyyy}");
                    col.Item().PaddingBottom(10);
                });

                page.Content().Column(col =>
                {
                    // xulase
                    col.Item().PaddingBottom(5).Text($"Acilis balansi: {s.OpeningBalance:0.00} {s.Currency}");
                    col.Item().Text($"Baglanis balansi: {s.ClosingBalance:0.00} {s.Currency}");
                    col.Item().Text($"Daxil olan: {s.TotalIn:0.00}   Cixan: {s.TotalOut:0.00}   Emeliyyat: {s.TransactionCount}");
                    col.Item().PaddingVertical(10).LineHorizontal(1);

                    // cedvel
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2);   // tarix
                            c.RelativeColumn(2);   // tip
                            c.RelativeColumn(3);   // tesvir
                            c.RelativeColumn(2);   // mebleg
                            c.RelativeColumn(2);   // balans
                        });

                        // basliq setri
                        table.Header(header =>
                        {
                            header.Cell().Text("Tarix").Bold();
                            header.Cell().Text("Tip").Bold();
                            header.Cell().Text("Tesvir").Bold();
                            header.Cell().Text("Mebleg").Bold();
                            header.Cell().Text("Balans").Bold();
                        });

                        foreach (var line in s.Lines)
                        {
                            var sign = line.Direction == "In" ? "+" : "-";
                            table.Cell().Text(line.Date.ToString("dd.MM.yyyy HH:mm"));
                            table.Cell().Text(line.Type);
                            table.Cell().Text(line.Description ?? "");
                            table.Cell().Text($"{sign}{line.Amount:0.00}");
                            table.Cell().Text($"{line.BalanceAfter:0.00}");
                        }
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Yaradildi: ");
                    x.Span(DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm"));
                });
            });
        });

        return doc.GeneratePdf();
    }
}