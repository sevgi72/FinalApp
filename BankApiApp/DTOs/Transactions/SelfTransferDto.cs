using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.Transaction;

public class SelfTransferDto
{
    [Required(ErrorMessage = "Gonderen hesab secilmelidir.")]
    public Guid FromAccountId { get; set; }

    [Required(ErrorMessage = "Alan hesab secilmelidir.")]
    public Guid ToAccountId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Mebleg 0-dan boyuk olmalidir.")]
    public decimal Amount { get; set; }
}