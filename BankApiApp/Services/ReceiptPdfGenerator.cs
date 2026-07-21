using BankApiApp.DTOs.Transactions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BankApiApp.Services;

public static class ReceiptPdfGenerator
{
    // ── Brend rengleri (frontla eyni ton) ──
    private const string Ink = "#2C2966";  // esas — derin benovseyi-goy
    private const string Ink2 = "#1B1940";  // daha tund
    private const string Ink3 = "#14122E";  // en tund
    private const string Neon = "#00E5D4";  // vurgu — cyan
    private const string Lilac = "#7C6FF0";  // komekci benovseyi
    private const string Mist = "#F4F5FA";  // aciq boz fon
    private const string Line = "#E6E8F2";  // serhed

    public static byte[] Generate(ReceiptDto r)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0);                    // kenar boslugu ozumuz idare edirik
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Ink2));

                // ══════════ BASLIQ (tund gradient zolaq) ══════════
                page.Header().Background(Ink).Padding(30).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Amorabank")
                            .FontSize(24).Bold().FontColor(Colors.White);
                        col.Item().PaddingTop(2).Text("Əməliyyat Qəbzi")
                            .FontSize(12).FontColor(Neon);
                    });

                    // sag terefde tarix
                    row.ConstantItem(160).AlignRight().Column(col =>
                    {
                        col.Item().Text("Referans")
                            .FontSize(8).FontColor("#FFFFFF99");
                        col.Item().Text(r.Reference)
                            .FontSize(11).SemiBold().FontColor(Colors.White);
                    });
                });

                // ══════════ MEZMUN ══════════
                page.Content().PaddingHorizontal(30).PaddingVertical(28).Column(col =>
                {
                    col.Spacing(0);

                    // ── Mebleg karti (aciq fonlu, merkezde) ──
                    col.Item().Background(Mist).Border(1).BorderColor(Line)
                        .CornerRadius(12).Padding(24).Column(card =>
                        {
                            card.Item().AlignCenter().Text("Məbləğ")
                                .FontSize(9).FontColor(Colors.Grey.Medium);
                            card.Item().AlignCenter().PaddingTop(4).Text($"{r.Amount:N2} ₼")
                                .FontSize(32).Bold().FontColor(Ink);

                            // status nisani
                            var isDone = r.Status == "Completed";
                            card.Item().AlignCenter().PaddingTop(10)
                                .Background(isDone ? "#00E5D41A" : "#FEE2E2")
                                .CornerRadius(20).PaddingVertical(5).PaddingHorizontal(14)
                                .Text(r.Status)
                                .FontSize(10).SemiBold()
                                .FontColor(isDone ? "#0F766E" : Colors.Red.Darken1);
                        });

                    col.Item().PaddingTop(24).Text("Əməliyyat detalları")
                        .FontSize(11).SemiBold().FontColor(Ink);

                    col.Item().PaddingTop(10).Column(rows =>
                    {
                        int i = 0;
                        DetailRow(rows, "Tarix", r.Date.ToString("dd.MM.yyyy HH:mm"), ref i);
                        DetailRow(rows, "Əməliyyat növü", r.Type, ref i);
                        if (!string.IsNullOrEmpty(r.FromAccount))
                            DetailRow(rows, "Göndərən", r.FromAccount!, ref i);
                        if (!string.IsNullOrEmpty(r.ToAccount))
                            DetailRow(rows, "Alan", r.ToAccount!, ref i);
                        if (!string.IsNullOrEmpty(r.Description))
                            DetailRow(rows, "Təsvir", r.Description!, ref i);
                    });
                });

                // ══════════ ALT QEYD ══════════
                page.Footer().PaddingHorizontal(30).PaddingBottom(24).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Line);
                    col.Item().PaddingTop(10).AlignCenter()
                        .Text("Bu, elektron qəbzdir və imza tələb etmir.")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().AlignCenter().PaddingTop(2)
                        .Text($"Yaradılma: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC")
                        .FontSize(8).FontColor(Colors.Grey.Lighten1);
                });
            });
        }).GeneratePdf();
    }

    // ── "Etiket / deyer" setri — cut setirler aciq fonlu (zebra) ──
    private static void DetailRow(ColumnDescriptor col, string label, string value, ref int index)
    {
        var bg = index % 2 == 0 ? Mist : "#FFFFFF";
        index++;

        col.Item().Background(bg).PaddingVertical(9).PaddingHorizontal(12).Row(row =>
        {
            row.ConstantItem(140).Text(label)
                .SemiBold().FontColor(Colors.Grey.Darken1);
            row.RelativeItem().Text(value).FontColor(Ink2);
        });
    }
}