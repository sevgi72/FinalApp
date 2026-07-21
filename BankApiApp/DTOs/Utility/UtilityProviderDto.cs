namespace BankApiApp.DTOs.Utility;

public class UtilityProviderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;

    // admin panelinde provayderin aktiv/deaktiv oldugunu gostermek ucun.
    // Adi istifadeci cavabinda null qalir (yalniz aktiv provayderler gelir).
    public bool? IsActive { get; set; }
}