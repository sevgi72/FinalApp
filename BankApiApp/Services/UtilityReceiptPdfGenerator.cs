using BankApiApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BankApiApp.Services;

public static class UtilityReceiptPdfGenerator
{
    public static byte[] Generate(UtilityPayment payment)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(30);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11));

                // başlıq
                page.Header().Column(col =>
                {
                    col.Item().Text("BankApiApp").FontSize(20).Bold().FontColor("#0F6E56");
                    col.Item().Text("Kommunal ödəniş qəbzi").FontSize(12).FontColor("#555555");
                    col.Item().PaddingTop(5).LineHorizontal(1).LineColor("#5DCAA5");
                });

                // məzmun
                page.Content().PaddingVertical(20).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Provayder:").FontColor("#555555");
                        row.RelativeItem().AlignRight().Text(payment.Provider.Name).Bold();
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Kateqoriya:").FontColor("#555555");
                        row.RelativeItem().AlignRight().Text(payment.Provider.Category.ToString());
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Abunəçi kodu:").FontColor("#555555");
                        row.RelativeItem().AlignRight().Text(payment.SubscriberCode);
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Tarix:").FontColor("#555555");
                        row.RelativeItem().AlignRight().Text(payment.CreatedAt.ToString("dd.MM.yyyy HH:mm"));
                    });
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Status:").FontColor("#555555");
                        row.RelativeItem().AlignRight().Text(payment.Status).FontColor("#0F6E56").Bold();
                    });

                    col.Item().PaddingTop(10).LineHorizontal(1).LineColor("#dddddd");

                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text("Ödənilən məbləğ:").FontSize(13).Bold();
                        row.RelativeItem().AlignRight().Text($"{payment.Amount:0.00} AZN").FontSize(13).Bold().FontColor("#0F6E56");
                    });
                });

                // alt
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Qəbz nömrəsi: ").FontColor("#999999").FontSize(9);
                    x.Span(payment.Id.ToString("N")[..12].ToUpper()).FontColor("#999999").FontSize(9);
                });
            });
        });

        return document.GeneratePdf();
    }
}