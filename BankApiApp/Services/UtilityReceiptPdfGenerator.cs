using BankApiApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BankApiApp.Services;

public static class UtilityReceiptPdfGenerator
{
    // ── Brend rengleri (o biri qebzle eyni ton) ──
    private const string Ink = "#2C2966";  // esas — derin benovseyi-goy
    private const string Ink2 = "#1B1940";  // daha tund
    private const string Neon = "#00E5D4";  // vurgu — cyan
    private const string Mist = "#F4F5FA";  // aciq boz fon
    private const string Line = "#E6E8F2";  // serhed

    public static byte[] Generate(UtilityPayment payment)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(0);                    // kenar boslugu ozumuz idare edirik
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Ink2));

                // ══════════ BASLIQ (tund zolaq) ══════════
                page.Header().Background(Ink).Padding(24).Column(col =>
                {
                    col.Item().Text("Amorabank")
                        .FontSize(20).Bold().FontColor(Colors.White);
                    col.Item().PaddingTop(2).Text("Kommunal ödəniş qəbzi")
                        .FontSize(11).FontColor(Neon);
                });

                // ══════════ MEZMUN ══════════
                page.Content().PaddingHorizontal(24).PaddingVertical(22).Column(col =>
                {
                    col.Spacing(0);

                    // ── detal setirleri (zebra) ──
                    int i = 0;
                    DetailRow(col, "Provayder", payment.Provider.Name, ref i, bold: true);
                    DetailRow(col, "Kateqoriya", payment.Provider.Category.ToString(), ref i);
                    DetailRow(col, "Abunəçi kodu", payment.SubscriberCode, ref i);
                    DetailRow(col, "Tarix", payment.CreatedAt.ToString("dd.MM.yyyy HH:mm"), ref i);

                    // status — rengli deyer
                    var isDone = payment.Status == "Completed";
                    col.Item().Background(i % 2 == 0 ? Mist : "#FFFFFF")
                        .PaddingVertical(9).PaddingHorizontal(12).Row(row =>
                        {
                            row.RelativeItem().Text("Status").FontColor(Colors.Grey.Darken1);
                            row.RelativeItem().AlignRight().Text(payment.Status)
                                .SemiBold().FontColor(isDone ? "#0F766E" : Colors.Red.Darken1);
                        });

                    // ── odenilen mebleg — vurgulu kart ──
                    col.Item().PaddingTop(18).Background(Mist).Border(1).BorderColor(Line)
                        .CornerRadius(12).Padding(18).Row(row =>
                        {
                            row.RelativeItem().AlignMiddle().Text("Ödənilən məbləğ")
                                .FontSize(12).SemiBold().FontColor(Ink);
                            row.RelativeItem().AlignRight().Text($"{payment.Amount:0.00} ₼")
                                .FontSize(18).Bold().FontColor(Ink);
                        });
                });

                // ══════════ ALT QEYD ══════════
                page.Footer().PaddingHorizontal(24).PaddingBottom(20).Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Line);
                    col.Item().PaddingTop(8).AlignCenter().Text(x =>
                    {
                        x.Span("Qəbz nömrəsi: ").FontColor(Colors.Grey.Medium).FontSize(9);
                        x.Span(payment.Id.ToString("N")[..12].ToUpper())
                            .FontColor(Colors.Grey.Darken1).FontSize(9).SemiBold();
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    // ── "Etiket / deyer" setri — cut setirler aciq fonlu (zebra) ──
    private static void DetailRow(ColumnDescriptor col, string label, string value, ref int index, bool bold = false)
    {
        var bg = index % 2 == 0 ? Mist : "#FFFFFF";
        index++;

        col.Item().Background(bg).PaddingVertical(9).PaddingHorizontal(12).Row(row =>
        {
            row.RelativeItem().Text(label).FontColor(Colors.Grey.Darken1);
            var cell = row.RelativeItem().AlignRight().Text(value);
            if (bold) cell.SemiBold().FontColor(Ink2);
        });
    }
}