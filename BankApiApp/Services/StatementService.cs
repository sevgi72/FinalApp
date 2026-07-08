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
    // hesabati PDF kimi yarat (peskar bank cixarisi dizayni)
    public async Task<byte[]> GeneratePdfAsync(string userId, StatementRequestDto dto)
    {
        var s = await GenerateAsync(userId, dto);
        QuestPDF.Settings.License = LicenseType.Community;

        // reng palitrasi
        string primary = "#1B3A5B";    // tund mavi (bank rengi)
        string accent = "#2E7D5B";     // yasil (menfi/musbet ucun)
        string lightGray = "#F5F6F8";
        string midGray = "#8A94A6";

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#2C3E50"));

                // ===== HEADER (rengli zolaq) =====
                page.Header().Background(primary).Padding(25).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("AmoraBank").FontSize(22).Bold().FontColor("#FFFFFF");
                        col.Item().Text("Hesab Cixarisi / Account Statement")
                            .FontSize(11).FontColor("#B8C5D6");
                    });

                    row.ConstantItem(180).AlignRight().Column(col =>
                    {
                        col.Item().Text($"Hesab: {s.AccountNumber}")
                            .FontSize(10).FontColor("#FFFFFF").Bold();
                        col.Item().Text($"Valyuta: {s.Currency}")
                            .FontSize(9).FontColor("#B8C5D6");
                        col.Item().Text($"{s.From:dd.MM.yyyy} - {s.To:dd.MM.yyyy}")
                            .FontSize(9).FontColor("#B8C5D6");
                    });
                });

                // ===== CONTENT =====
                page.Content().PaddingHorizontal(25).PaddingTop(20).Column(col =>
                {
                    // --- xulase kartlari ---
                    col.Item().PaddingBottom(15).Row(row =>
                    {
                        // acilis balansi
                        row.RelativeItem().Background(lightGray).Padding(12).Column(c =>
                        {
                            c.Item().Text("ACILIS BALANSI").FontSize(8).FontColor(midGray).Bold();
                            c.Item().PaddingTop(3).Text($"{s.OpeningBalance:0.00} {s.Currency}")
                                .FontSize(13).Bold().FontColor(primary);
                        });

                        row.ConstantItem(10);

                        // baglanis balansi
                        row.RelativeItem().Background(lightGray).Padding(12).Column(c =>
                        {
                            c.Item().Text("BAGLANIS BALANSI").FontSize(8).FontColor(midGray).Bold();
                            c.Item().PaddingTop(3).Text($"{s.ClosingBalance:0.00} {s.Currency}")
                                .FontSize(13).Bold().FontColor(primary);
                        });
                    });

                    // --- daxil/cixan/say ---
                    col.Item().PaddingBottom(20).Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor("#E0E4EA").Padding(10).Column(c =>
                        {
                            c.Item().Text("DAXIL OLAN").FontSize(8).FontColor(midGray).Bold();
                            c.Item().PaddingTop(2).Text($"+{s.TotalIn:0.00}").FontSize(12).Bold().FontColor(accent);
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Border(1).BorderColor("#E0E4EA").Padding(10).Column(c =>
                        {
                            c.Item().Text("CIXAN").FontSize(8).FontColor(midGray).Bold();
                            c.Item().PaddingTop(2).Text($"-{s.TotalOut:0.00}").FontSize(12).Bold().FontColor("#C0392B");
                        });
                        row.ConstantItem(10);
                        row.RelativeItem().Border(1).BorderColor("#E0E4EA").Padding(10).Column(c =>
                        {
                            c.Item().Text("EMELIYYAT SAYI").FontSize(8).FontColor(midGray).Bold();
                            c.Item().PaddingTop(2).Text($"{s.TransactionCount}").FontSize(12).Bold().FontColor(primary);
                        });
                    });

                    // --- emeliyyatlar cedveli ---
                    col.Item().Text("EMELIYYATLAR").FontSize(10).Bold().FontColor(primary);
                    col.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2.2f);   // tarix
                            c.RelativeColumn(1.5f);   // tip
                            c.RelativeColumn(3f);     // tesvir
                            c.RelativeColumn(1.8f);   // mebleg
                            c.RelativeColumn(1.8f);   // balans
                        });

                        // basliq setri (rengli fon)
                        table.Header(header =>
                        {
                            static IContainer HeaderCell(IContainer c, string bg) =>
                                c.Background(bg).PaddingVertical(6).PaddingHorizontal(6);

                            header.Cell().Element(x => HeaderCell(x, primary)).Text("Tarix").FontColor("#FFFFFF").Bold().FontSize(8);
                            header.Cell().Element(x => HeaderCell(x, primary)).Text("Tip").FontColor("#FFFFFF").Bold().FontSize(8);
                            header.Cell().Element(x => HeaderCell(x, primary)).Text("Tesvir").FontColor("#FFFFFF").Bold().FontSize(8);
                            header.Cell().Element(x => HeaderCell(x, primary)).AlignRight().Text("Mebleg").FontColor("#FFFFFF").Bold().FontSize(8);
                            header.Cell().Element(x => HeaderCell(x, primary)).AlignRight().Text("Balans").FontColor("#FFFFFF").Bold().FontSize(8);
                        });

                        // setrler (zebra fon)
                        int i = 0;
                        foreach (var line in s.Lines)
                        {
                            var rowBg = (i % 2 == 0) ? "#FFFFFF" : lightGray;
                            var isIn = line.Direction == "In";
                            var sign = isIn ? "+" : "-";
                            var amountColor = isIn ? accent : "#C0392B";

                            table.Cell().Background(rowBg).PaddingVertical(5).PaddingHorizontal(6)
                                .Text(line.Date.ToString("dd.MM.yyyy HH:mm")).FontSize(8);
                            table.Cell().Background(rowBg).PaddingVertical(5).PaddingHorizontal(6)
                                .Text(line.Type).FontSize(8);
                            table.Cell().Background(rowBg).PaddingVertical(5).PaddingHorizontal(6)
                                .Text(line.Description ?? "").FontSize(8);
                            table.Cell().Background(rowBg).PaddingVertical(5).PaddingHorizontal(6)
                                .AlignRight().Text($"{sign}{line.Amount:0.00}").FontSize(8).Bold().FontColor(amountColor);
                            table.Cell().Background(rowBg).PaddingVertical(5).PaddingHorizontal(6)
                                .AlignRight().Text($"{line.BalanceAfter:0.00}").FontSize(8).Bold();

                            i++;
                        }
                    });

                    // bos emeliyyat halinda mesaj
                    if (s.Lines.Count == 0)
                    {
                        col.Item().PaddingTop(20).AlignCenter()
                            .Text("Bu dovr ucun emeliyyat yoxdur.").FontSize(10).FontColor(midGray);
                    }
                });

                // ===== FOOTER =====
                page.Footer().BorderTop(1).BorderColor("#E0E4EA").PaddingTop(8).PaddingHorizontal(25).Row(row =>
                {
                    row.RelativeItem().Text(x =>
                    {
                        x.Span("Yaradildi: ").FontSize(7).FontColor(midGray);
                        x.Span(DateTime.UtcNow.ToString("dd.MM.yyyy HH:mm")).FontSize(7).FontColor(midGray);
                        x.Span("  |  AmoraBank resmi senedi").FontSize(7).FontColor(midGray);
                    });

                    row.ConstantItem(100).AlignRight().Text(x =>
                    {
                        x.Span("Sehife ").FontSize(7).FontColor(midGray);
                        x.CurrentPageNumber().FontSize(7).FontColor(midGray);
                        x.Span(" / ").FontSize(7).FontColor(midGray);
                        x.TotalPages().FontSize(7).FontColor(midGray);
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}