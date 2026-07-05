using System.ComponentModel.DataAnnotations;

namespace BankApiApp.DTOs.RoundUp;

public class EnableRoundUpDto
{
    [Required(ErrorMessage = "Menbe hesab secilmelidir.")]
    public Guid SourceAccountId { get; set; }

    [Required(ErrorMessage = "Yigim hesabi secilmelidir.")]
    public Guid TargetAccountId { get; set; }

    [Range(1, 5, ErrorMessage = "Yuvarlaqlasdirma 1 ve ya 5 ola biler.")]
    public int RoundTo { get; set; } = 1;
}