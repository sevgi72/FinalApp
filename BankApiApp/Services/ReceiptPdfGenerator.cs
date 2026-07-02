using BankApiApp.DTOs.Transactions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BankApiApp.Services;

public static class ReceiptPdfGenerator
{
    public static byte[] Generate(ReceiptDto r)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Grey.Darken3));

                // basliq
                page.Header().Column(col =>
                {
                    col.Item().Text("BankApiApp").FontSize(22).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().Text("Emeliyyat Qebzi").FontSize(13).FontColor(Colors.Grey.Medium);
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                });

                // mezmun
                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(10);

                    // mebleg — boyuk ve merkezde
                    col.Item().AlignCenter().Text($"{r.Amount:N2} AZN")
                        .FontSize(28).Bold().FontColor(Colors.Blue.Darken2);

                    // status — rengli
                    col.Item().AlignCenter().Text(r.Status)
                        .FontSize(12)
                        .FontColor(r.Status == "Completed" ? Colors.Green.Darken1 : Colors.Red.Darken1);

                    // detal setirleri
                    Row(col, "Referans:", r.Reference);
                    Row(col, "Tarix:", r.Date.ToString("dd.MM.yyyy HH:mm"));
                    Row(col, "Emeliyyat novu:", r.Type);
                    if (!string.IsNullOrEmpty(r.FromAccount)) Row(col, "Gonderen:", r.FromAccount!);
                    if (!string.IsNullOrEmpty(r.ToAccount)) Row(col, "Alan:", r.ToAccount!);
                    if (!string.IsNullOrEmpty(r.Description)) Row(col, "Tesvir:", r.Description!);
                });

                // alt qeyd
                page.Footer().Column(col =>
                {
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    col.Item().PaddingTop(8).AlignCenter()
                        .Text("Bu, elektron qebzdir ve imza teleb etmir.")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                    col.Item().AlignCenter()
                        .Text($"Yaradilma: {DateTime.UtcNow:dd.MM.yyyy HH:mm} UTC")
                        .FontSize(9).FontColor(Colors.Grey.Medium);
                });
            });
        }).GeneratePdf();   // byte[] qaytarir
    }

    // bir "etiket: deyer" setri cekir
    private static void Row(ColumnDescriptor col, string label, string value)
    {
        col.Item().Row(row =>
        {
            row.ConstantItem(140).Text(label).SemiBold().FontColor(Colors.Grey.Darken1);
            row.RelativeItem().Text(value);
        });
    }
}