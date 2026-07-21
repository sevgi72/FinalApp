using BankApiApp.Models;

namespace BankApiApp.DTOs.Utility;

// SuperAdmin yeni kommunal provayder yaratanda gonderdiyi melumat
public class CreateProviderDto
{
    public string Name { get; set; } = string.Empty;   // mes. "Azerisiq"
    public UtilityCategory Category { get; set; }       // Electricity=1 ... TV=6
}